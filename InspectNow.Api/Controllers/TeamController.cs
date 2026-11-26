using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using InspectNow.Api.Authorization;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/team")]
public sealed class TeamController(InspectNowDbContext db, OrganizationScope scope, TimeProvider clock, PlatformOwner owner) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = OrganizationAccess.Policy)]
    [ManagerOnly]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var members = await db.OrganizationMemberships.AsNoTracking()
            .Where(member => member.OrganizationId == scope.Id && member.Issuer == OrganizationAccess.LocalIssuer)
            .Join(db.Users, member => member.Subject, user => user.Id,
                (member, user) => new { user.Id, user.Email, Role = member.Role.ToString(),
                    member.CustomRoleIds,
                    CanUseForms = (member.AllowedModules & WorkspaceModules.Forms) != 0,
                    CanUseInspections = (member.AllowedModules & WorkspaceModules.Inspections) != 0 })
            .OrderBy(member => member.Email)
            .ToListAsync(cancellationToken);
        return Ok(members);
    }

    [HttpGet("roles")]
    [Authorize(Policy = OrganizationAccess.Policy)]
    [ManagerOnly]
    public async Task<IActionResult> Roles(CancellationToken cancellationToken) => Ok(
        await db.OrganizationRoles.AsNoTracking().OrderBy(role => role.Name)
            .Select(role => new { role.Id, role.Name }).ToListAsync(cancellationToken));

    [HttpPost("roles")]
    [Authorize(Policy = OrganizationAccess.Policy)]
    [AdminOnly]
    public async Task<IActionResult> CreateRole(RoleInput input, CancellationToken cancellationToken)
    {
        var name = input.Name.Trim();
        if (name.Length == 0 || Enum.GetNames<WorkspaceRole>().Contains(name, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { detail = "Choose a name distinct from the built-in roles." });
        var role = new OrganizationRole(scope.Id!.Value, name);
        db.OrganizationRoles.Add(role);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { return Conflict(new { detail = "A role with that name already exists in this workspace." }); }
        return StatusCode(201, new { role.Id, role.Name });
    }

    [HttpPut("{userId}/roles")]
    [Authorize(Policy = OrganizationAccess.Policy)]
    [AdminOnly]
    public async Task<IActionResult> AssignRoles(string userId, MemberRolesInput input, CancellationToken cancellationToken)
    {
        var ids = input.RoleIds.Distinct().ToArray();
        if (await db.OrganizationRoles.CountAsync(role => ids.Contains(role.Id), cancellationToken) != ids.Length)
            return BadRequest(new { detail = "Choose roles from this workspace." });
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var member = await db.LockMembership(scope.Id!.Value, OrganizationAccess.LocalIssuer, userId, cancellationToken);
        if (member is null) return NotFound();
        member.SetCustomRoles(ids);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPut("{userId}/access")]
    [Authorize(Policy = OrganizationAccess.Policy)]
    [AdminOnly]
    public async Task<IActionResult> UpdateAccess(string userId, AccessInput input, CancellationToken cancellationToken)
    {
        if (input.Role is not (nameof(WorkspaceRole.Manager) or nameof(WorkspaceRole.Inspector)))
            return BadRequest(new { detail = "Choose Manager or Inspector." });
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var member = await db.LockMembership(scope.Id!.Value, OrganizationAccess.LocalIssuer, userId, cancellationToken);
        if (member is null) return NotFound();
        if (member.Role == WorkspaceRole.Admin) return Forbid();
        var targetRole = Enum.Parse<WorkspaceRole>(input.Role);
        var modules = (input.CanUseForms ? WorkspaceModules.Forms : WorkspaceModules.None) |
            (input.CanUseInspections ? WorkspaceModules.Inspections : WorkspaceModules.None);
        if ((modules & ~scope.Modules) != WorkspaceModules.None)
            return BadRequest(new { detail = "Choose modules available in this workspace." });

        if (targetRole != WorkspaceRole.Inspector || !modules.HasFlag(WorkspaceModules.Inspections))
        {
            var hasOpenInspections = await db.Inspections.AnyAsync(inspection =>
                inspection.AssignedToUserId == userId && inspection.Status == InspectNow.Domain.Inspections.InspectionStatus.Draft,
                cancellationToken);
            var hasOpenActions = await db.CorrectiveActions.AnyAsync(action => action.AssigneeId == userId &&
                action.Status != InspectNow.Domain.Inspections.CorrectiveActionStatus.Closed, cancellationToken);
            if (hasOpenInspections || hasOpenActions)
                return Conflict(new { detail = "Finish this member’s assigned inspection work before changing their access." });
        }

        member.SetAccess(targetRole, modules);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict(new { detail = "Access changed. Reload the team list and try again." }); }
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations")]
    [Authorize(Policy = OrganizationAccess.Policy)]
    [AdminOnly]
    public async Task<IActionResult> Invite(InviteInput input, CancellationToken cancellationToken)
    {
        if (input.Role is not (nameof(WorkspaceRole.Inspector) or nameof(WorkspaceRole.Manager) or nameof(WorkspaceRole.Admin)))
            return BadRequest(new { detail = "Choose a supported workspace role." });
        if (input.Role == nameof(WorkspaceRole.Admin) && !owner.IsOwner(User))
            return Forbid();

        var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAtUtc = clock.GetUtcNow().AddDays(2);
        db.WorkspaceInvitations.Add(new WorkspaceInvitation(
            scope.Id!.Value, Hash(code), Enum.Parse<WorkspaceRole>(input.Role), expiresAtUtc));
        await db.SaveChangesAsync(cancellationToken);
        Response.Headers.CacheControl = "no-store";
        return StatusCode(201, new { code, expiresAtUtc });
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join(JoinInput input, CancellationToken cancellationToken)
    {
        var subject = OrganizationAccess.Subject(User);
        if (!OrganizationAccess.IsLocal(User) || subject is null) return Forbid();
        var codeHash = Hash(input.Code.Trim().ToUpperInvariant());
        var invite = await db.WorkspaceInvitations.SingleOrDefaultAsync(item => item.CodeHash == codeHash, cancellationToken);
        var now = clock.GetUtcNow();
        if (invite is null || invite.RedeemedAtUtc != null || invite.ExpiresAtUtc <= now)
            return BadRequest(new { detail = "The invitation is invalid, expired, or already used." });

        var alreadyMember = await OrganizationAccess.MembershipsFor(db, User)
            .AnyAsync(member => member.OrganizationId == invite.OrganizationId, cancellationToken);
        if (alreadyMember) return Conflict(new { detail = "You already belong to this workspace." });

        invite.RedeemedAtUtc = now;
        db.OrganizationMemberships.Add(new OrganizationMembership(
            invite.OrganizationId, OrganizationAccess.LocalIssuer, subject, invite.Role));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new { detail = "The invitation was already used." });
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { detail = "You already belong to this workspace." });
        }
        return Ok(new { invite.OrganizationId });
    }

    private static string Hash(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    public sealed record InviteInput([Required, StringLength(20)] string Role);
    public sealed record RoleInput([Required, StringLength(80)] string Name);
    public sealed record MemberRolesInput([Required, MaxLength(100)] Guid[] RoleIds);
    public sealed record AccessInput([Required, StringLength(20)] string Role, bool CanUseForms, bool CanUseInspections);
    public sealed record JoinInput([Required, StringLength(64, MinimumLength = 64)] string Code);
}
