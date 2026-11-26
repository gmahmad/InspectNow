namespace InspectNow.Domain.Organizations;

public sealed class WorkspaceInvitation(long organizationId, string codeHash, WorkspaceRole role, DateTimeOffset expiresAtUtc)
{
    public long Id { get; private set; }
    public long OrganizationId { get; private set; } = organizationId;
    public string CodeHash { get; private set; } = codeHash;
    public WorkspaceRole Role { get; private set; } = role;
    public DateTimeOffset ExpiresAtUtc { get; private set; } = expiresAtUtc;
    public DateTimeOffset? RedeemedAtUtc { get; set; }
}
