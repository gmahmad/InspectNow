using InspectNow.Api.Authorization;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InspectNow.Domain.Organizations;
using System.ComponentModel.DataAnnotations;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(InspectNowDbContext db, PlatformOwner owner) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<OrganizationSummary>> List(CancellationToken cancellationToken)
    {
        var memberships = OrganizationAccess.MembershipsFor(db, User);
        var organizations = await db.Organizations.AsNoTracking()
            .Join(memberships, organization => organization.Id, membership => membership.OrganizationId,
                (organization, membership) => new { organization.Id, organization.Name, organization.Type, membership.Role, membership.AllowedModules })
            .OrderBy(organization => organization.Name).ThenBy(organization => organization.Id)
            .ToListAsync(cancellationToken);
        return organizations.Select(organization =>
            Summary(organization.Id, organization.Name, organization.Type, organization.Role, organization.AllowedModules)).ToArray();
    }

    [HttpPost]
    public async Task<IActionResult> Create(OrganizationInput input, CancellationToken cancellationToken)
    {
        var subject = OrganizationAccess.Subject(User);
        if (!owner.IsOwner(User) || subject is null) return Forbid();
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { detail = "Workspace name is required." });

        if (!Enum.TryParse<OrganizationType>(input.Type ?? nameof(OrganizationType.General), out var type) ||
            (input.Type is not null && type.ToString() != input.Type))
            return BadRequest(new { detail = "Choose a supported organization type." });
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var organization = new Organization(input.Name, type);
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(cancellationToken);
        db.OrganizationMemberships.Add(new OrganizationMembership(
            organization.Id, OrganizationAccess.LocalIssuer, subject, WorkspaceRole.Admin));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return StatusCode(201, Summary(organization.Id, organization.Name, type, WorkspaceRole.Admin, WorkspaceModules.All));
    }

    private static OrganizationSummary Summary(long id, string name, OrganizationType type, WorkspaceRole role, WorkspaceModules allowed)
    {
        var modules = WorkspaceFeatures.For(type);
        var access = modules & allowed;
        return new OrganizationSummary(id, name, role.ToString(), type.ToString(),
            access.HasFlag(WorkspaceModules.Forms), access.HasFlag(WorkspaceModules.Inspections),
            modules.HasFlag(WorkspaceModules.Forms), modules.HasFlag(WorkspaceModules.Inspections));
    }

    public sealed record OrganizationInput([Required, StringLength(200)] string Name, [StringLength(30)] string? Type);
    public sealed record OrganizationSummary(long Id, string Name, string Role, string Type,
        bool CanUseForms, bool CanUseInspections, bool OffersForms, bool OffersInspections);
}
