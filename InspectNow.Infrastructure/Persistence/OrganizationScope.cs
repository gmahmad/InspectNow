using InspectNow.Domain.Organizations;

namespace InspectNow.Infrastructure.Persistence;

public sealed class OrganizationScope
{
    public long? Id { get; private set; }
    public WorkspaceRole Role { get; private set; } = WorkspaceRole.Manager;
    public string? Subject { get; private set; }
    public bool IsLocal { get; private set; }
    public WorkspaceModules Modules { get; private set; }
    public Guid[] CustomRoleIds { get; private set; } = [];

    public void Select(long id, WorkspaceRole role = WorkspaceRole.Manager, string? subject = null, bool isLocal = false,
        WorkspaceModules modules = WorkspaceModules.All, Guid[]? customRoleIds = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        if (Id.HasValue)
            throw new InvalidOperationException("An organization has already been selected for this request.");
        Id = id;
        Role = role;
        Subject = subject;
        IsLocal = isLocal;
        Modules = modules;
        CustomRoleIds = customRoleIds ?? [];
    }
}
