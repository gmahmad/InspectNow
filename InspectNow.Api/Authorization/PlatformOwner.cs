using System.Security.Claims;

namespace InspectNow.Api.Authorization;

public sealed class PlatformOwner(IConfiguration configuration)
{
    public bool IsOwner(ClaimsPrincipal user) =>
        OrganizationAccess.IsLocal(user) &&
        configuration["Platform:OwnerUserId"] is { Length: > 0 } ownerId &&
        string.Equals(OrganizationAccess.Subject(user), ownerId, StringComparison.Ordinal);
}
