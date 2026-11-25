using System.Globalization;
using System.Security.Claims;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Api.Authorization;

public sealed class OrganizationAccess : IAuthorizationRequirement
{
    public const string Policy = "OrganizationMember";
    public const string Header = "X-Organization-Id";

    public static IQueryable<OrganizationMembership> MembershipsFor(
        InspectNowDbContext db, ClaimsPrincipal user)
    {
        var issuer = user.FindFirstValue("iss");
        var subject = user.FindFirstValue("sub");
        return db.OrganizationMemberships.AsNoTracking()
            .Where(membership => membership.Issuer == issuer && membership.Subject == subject);
    }
}

public sealed class OrganizationAccessHandler(InspectNowDbContext db, OrganizationScope scope)
    : AuthorizationHandler<OrganizationAccess>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrganizationAccess requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true || context.Resource is not HttpContext http ||
            !http.Request.Headers.TryGetValue(OrganizationAccess.Header, out var header) || header.Count != 1 ||
            !long.TryParse(header[0], NumberStyles.None, CultureInfo.InvariantCulture, out var organizationId) ||
            organizationId <= 0)
        {
            return;
        }

        var isMember = await OrganizationAccess.MembershipsFor(db, context.User)
            .AnyAsync(membership => membership.OrganizationId == organizationId, http.RequestAborted);
        if (isMember)
        {
            scope.Select(organizationId);
            context.Succeed(requirement);
        }
    }
}
