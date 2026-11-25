using InspectNow.Api.Authorization;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public sealed class OrganizationsController(InspectNowDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyList<OrganizationSummary>> List(CancellationToken cancellationToken)
    {
        var memberships = OrganizationAccess.MembershipsFor(db, User);
        return await db.Organizations.AsNoTracking()
            .Where(organization => memberships.Any(membership => membership.OrganizationId == organization.Id))
            .OrderBy(organization => organization.Name).ThenBy(organization => organization.Id)
            .Select(organization => new OrganizationSummary(organization.Id, organization.Name))
            .ToListAsync(cancellationToken);
    }

    public sealed record OrganizationSummary(long Id, string Name);
}
