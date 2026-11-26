using System.Globalization;
using System.Security.Claims;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Api.Authorization;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class ManagerOnlyAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class AdminOnlyAttribute : Attribute;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public sealed class RequireWorkspaceModuleAttribute(WorkspaceModules module) : Attribute
{
    public WorkspaceModules Module { get; } = module;
}

public sealed class OrganizationAccess : IAuthorizationRequirement
{
    public const string Policy = "OrganizationMember";
    public const string Header = "X-Organization-Id";
    public const string LocalIssuer = "inspectnow-local";

    public static bool IsLocal(ClaimsPrincipal user) =>
        user.Identity?.AuthenticationType == IdentityConstants.ApplicationScheme;

    public static string? Subject(ClaimsPrincipal user) =>
        user.FindFirstValue(IsLocal(user) ? ClaimTypes.NameIdentifier : "sub");

    public static IQueryable<OrganizationMembership> MembershipsFor(
        InspectNowDbContext db, ClaimsPrincipal user)
    {
        var issuer = IsLocal(user) ? LocalIssuer : user.FindFirstValue("iss");
        var subject = Subject(user);
        if (!IsLocal(user) && issuer == LocalIssuer)
            return db.OrganizationMemberships.Where(_ => false);
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

        var membership = await OrganizationAccess.MembershipsFor(db, context.User)
            .Where(member => member.OrganizationId == organizationId)
            .Join(db.Organizations, member => member.OrganizationId, organization => organization.Id,
                (member, organization) => new { member.Role, member.Subject, member.AllowedModules, member.CustomRoleIds, organization.Type })
            .SingleOrDefaultAsync(http.RequestAborted);
        if (membership is not null)
        {
            var metadata = http.GetEndpoint()?.Metadata;
            var modules = WorkspaceFeatures.For(membership.Type) & membership.AllowedModules;
            if (metadata?.GetMetadata<RequireWorkspaceModuleAttribute>() is { } requirementModule &&
                !modules.HasFlag(requirementModule.Module)) return;
            if ((metadata?.GetMetadata<ManagerOnlyAttribute>() is not null && membership.Role == WorkspaceRole.Inspector) ||
                (metadata?.GetMetadata<AdminOnlyAttribute>() is not null && membership.Role != WorkspaceRole.Admin))
                return;

            scope.Select(organizationId, membership.Role, membership.Subject, OrganizationAccess.IsLocal(context.User), modules, membership.CustomRoleIds);
            context.Succeed(requirement);
        }
    }
}
