namespace InspectNow.Domain.Organizations;

public sealed class OrganizationMembership
{
    public const int MaxIdentityPartLength = 255;

    public long OrganizationId { get; private set; }
    public string Issuer { get; private set; }
    public string Subject { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public WorkspaceModules AllowedModules { get; private set; } = WorkspaceModules.All;
    public Guid[] CustomRoleIds { get; private set; } = [];

    public void SetCustomRoles(Guid[] ids) => CustomRoleIds = ids;

    public void SetAccess(WorkspaceRole role, WorkspaceModules modules)
    {
        if (Role == WorkspaceRole.Admin || role == WorkspaceRole.Admin)
            throw new InvalidOperationException("Workspace administrators are provisioned by the platform owner.");
        Role = role;
        AllowedModules = modules;
    }

    public OrganizationMembership(long organizationId, string issuer, string subject, WorkspaceRole role = WorkspaceRole.Manager)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(organizationId);
        ValidateIdentityPart(issuer, nameof(issuer));
        ValidateIdentityPart(subject, nameof(subject));
        OrganizationId = organizationId;
        Issuer = issuer;
        Subject = subject;
        Role = role;
    }

    private static void ValidateIdentityPart(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > MaxIdentityPartLength)
            throw new ArgumentException($"Identity values cannot exceed {MaxIdentityPartLength} characters.", parameterName);
    }
}
