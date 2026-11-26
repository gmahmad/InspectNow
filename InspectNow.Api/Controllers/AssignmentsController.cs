using System.ComponentModel.DataAnnotations;
using InspectNow.Api.Authorization;
using InspectNow.Domain.Inspections;
using InspectNow.Domain.Organizations;
using InspectNow.Domain.Templates;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationAccess.Policy)]
[RequireWorkspaceModule(WorkspaceModules.Inspections)]
[Route("api/assignments")]
public sealed class AssignmentsController(InspectNowDbContext db, OrganizationScope scope, TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var items = await db.Inspections.AsNoTracking().Where(inspection => inspection.SiteId != null)
            .OrderBy(inspection => inspection.Status == InspectionStatus.Submitted)
            .ThenBy(inspection => inspection.DueDate).ThenByDescending(inspection => inspection.Id)
            .Select(inspection => new
            {
                inspection.Id, inspection.SiteName, inspection.TemplateName, inspection.DueDate,
                Status = inspection.Status.ToString(),
                Assignee = db.Users.Where(user => user.Id == inspection.AssignedToUserId).Select(user => user.Email).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return Ok(items);
    }

    [HttpPost]
    [ManagerOnly]
    public async Task<IActionResult> Create(AssignmentInput input, CancellationToken cancellationToken)
    {
        var template = await db.InspectionTemplates.Include(item => item.Questions)
            .SingleOrDefaultAsync(item => item.Id == input.TemplateId, cancellationToken);
        var site = await db.Sites.SingleOrDefaultAsync(item => item.Id == input.SiteId, cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var member = await db.LockMembership(scope.Id!.Value, OrganizationAccess.LocalIssuer, input.AssigneeId, cancellationToken);
        var assignee = member?.Role == WorkspaceRole.Inspector && member.AllowedModules.HasFlag(WorkspaceModules.Inspections) &&
            await db.Users.AnyAsync(user => user.Id == input.AssigneeId, cancellationToken);
        if (site is null || template is null || !assignee)
            return NotFound(new { detail = "Choose a site, checklist, and inspector from this workspace." });
        if (template.Status != TemplateStatus.Published)
            return Conflict(new { detail = "Publish the checklist before assigning it." });
        if (input.DueDate == default)
            return BadRequest(new { detail = "Choose a due date." });

        var inspection = Inspection.Start(template, site.Name, clock.GetUtcNow());
        inspection.Assign(site.Id, input.AssigneeId, input.DueDate);
        db.AddInspection(inspection);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(201, new { inspection.Id });
    }

    public sealed record AssignmentInput(
        [Range(1L, long.MaxValue)] long TemplateId,
        [Range(1L, long.MaxValue)] long SiteId,
        [Required, StringLength(255)] string AssigneeId,
        DateOnly DueDate);
}
