using System.ComponentModel.DataAnnotations;
using InspectNow.Api.Authorization;
using InspectNow.Domain.Inspections;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationAccess.Policy)]
[RequireWorkspaceModule(WorkspaceModules.Inspections)]
[Route("api/corrective-actions")]
public sealed class CorrectiveActionsController(InspectNowDbContext db, OrganizationScope scope, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery, Range(1, 1_000_000)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = db.CorrectiveActions.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(action => action.Status == CorrectiveActionStatus.Closed)
            .ThenBy(action => action.DueDate).ThenBy(action => action.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(action => new
            {
                action.Id, action.InspectionId, action.QuestionId, action.SiteName, action.Finding,
                FindingNote = db.Set<InspectionQuestion>().Where(question => question.Id == action.QuestionId)
                    .Select(question => question.Comment).FirstOrDefault(),
                action.Instructions, action.DueDate, Status = action.Status.ToString(), action.Resolution,
                action.ReviewNote, action.ClosedAtUtc, action.Version,
                Assignee = db.Users.Where(user => user.Id == action.AssigneeId).Select(user => user.Email).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return Ok(new { items, totalPages = (int)Math.Ceiling((double)totalCount / pageSize) });
    }

    [HttpPost]
    [ManagerOnly]
    public async Task<IActionResult> Create(CreateInput input, CancellationToken cancellationToken)
    {
        var inspection = await db.Inspections.Include(item => item.Questions)
            .SingleOrDefaultAsync(item => item.Id == input.InspectionId, cancellationToken);
        var question = inspection?.Questions.SingleOrDefault(item => item.Id == input.QuestionId);
        if (inspection is null || question is null) return NotFound();
        if (inspection.Status != InspectionStatus.Submitted || question.Answer != InspectionAnswer.Fail)
            return Conflict(new { detail = "Select a failed answer on a submitted inspection." });
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var member = await db.LockMembership(scope.Id!.Value, OrganizationAccess.LocalIssuer, input.AssigneeId, cancellationToken);
        var isInspector = member?.Role == WorkspaceRole.Inspector && member.AllowedModules.HasFlag(WorkspaceModules.Inspections) &&
            await db.Users.AnyAsync(user => user.Id == input.AssigneeId, cancellationToken);
        if (!isInspector) return NotFound(new { detail = "Choose an inspector from this workspace." });
        if (input.DueDate == default || string.IsNullOrWhiteSpace(input.Instructions))
            return BadRequest(new { detail = "Instructions and a due date are required." });

        var action = CorrectiveAction.Create(inspection, question.Id, input.Instructions.Trim(),
            input.AssigneeId, input.DueDate, clock.GetUtcNow());
        db.CorrectiveActions.Add(action);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "IX_CorrectiveActions_QuestionId" })
        {
            return Conflict(new { detail = "A corrective action already exists for this answer." });
        }
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(201, new { action.Id });
    }

    [HttpPost("{id:long:min(1)}/resolve")]
    public async Task<IActionResult> Resolve(long id, ResolveInput input, CancellationToken cancellationToken)
    {
        var action = await db.CorrectiveActions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (action is null) return NotFound();
        if (!scope.IsLocal || scope.Subject != action.AssigneeId) return Forbid();
        if (string.IsNullOrWhiteSpace(input.Resolution)) return BadRequest(new { detail = "Describe what was fixed." });
        if (action.Version != input.ExpectedVersion || action.Status != CorrectiveActionStatus.Open)
            return StaleAction();
        action.Resolve(input.Resolution.Trim());
        return await SaveTransition(cancellationToken);
    }

    [HttpPost("{id:long:min(1)}/review")]
    [ManagerOnly]
    public async Task<IActionResult> Review(long id, ReviewInput input, CancellationToken cancellationToken)
    {
        var action = await db.CorrectiveActions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (action is null) return NotFound();
        if (input.Approve is null || (!input.Approve.Value && string.IsNullOrWhiteSpace(input.Note)))
            return BadRequest(new { detail = "Choose a review decision and explain any work being returned." });
        if (action.Version != input.ExpectedVersion || action.Status != CorrectiveActionStatus.ReadyForReview)
            return StaleAction();
        action.Review(input.Approve.Value, input.Note?.Trim(), clock.GetUtcNow());
        return await SaveTransition(cancellationToken);
    }

    private async Task<IActionResult> SaveTransition(CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return StaleAction(); }
        return NoContent();
    }

    private IActionResult StaleAction() => Conflict(new { detail = "This action changed. Reload it and try again." });

    public sealed record CreateInput(
        [Range(1L, long.MaxValue)] long InspectionId,
        [Range(1L, long.MaxValue)] long QuestionId,
        [Required, StringLength(255)] string AssigneeId,
        [Required, StringLength(2000)] string Instructions,
        DateOnly DueDate);
    public sealed record ResolveInput([Required, StringLength(2000)] string Resolution, Guid ExpectedVersion);
    public sealed record ReviewInput(bool? Approve, [StringLength(2000)] string? Note, Guid ExpectedVersion);
}
