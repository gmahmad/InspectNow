using System.ComponentModel.DataAnnotations;
using InspectNow.Api.Authorization;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Api.Controllers;

[ApiController]
[Authorize(Policy = OrganizationAccess.Policy)]
[RequireWorkspaceModule(WorkspaceModules.Inspections)]
[ManagerOnly]
[Route("api/sites")]
public sealed class SitesController(InspectNowDbContext db, OrganizationScope scope) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await db.Sites.AsNoTracking().OrderBy(site => site.Name).ThenBy(site => site.Id)
            .Select(site => new { site.Id, site.Name }).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(SiteInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { detail = "Site name is required." });
        var site = new Site(scope.Id!.Value, input.Name.Trim());
        db.Sites.Add(site);
        await db.SaveChangesAsync(cancellationToken);
        return StatusCode(201, new { site.Id, site.Name });
    }

    public sealed record SiteInput([Required, StringLength(200)] string Name);
}
