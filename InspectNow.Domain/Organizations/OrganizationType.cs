namespace InspectNow.Domain.Organizations;

public enum OrganizationType { General, FieldOperations, Education, ClientServices }

[Flags]
public enum WorkspaceModules { None = 0, Forms = 1, Inspections = 2, All = Forms | Inspections }

public static class WorkspaceFeatures
{
    public static WorkspaceModules For(OrganizationType type) => type switch
    {
        OrganizationType.General or OrganizationType.FieldOperations => WorkspaceModules.All,
        OrganizationType.Education or OrganizationType.ClientServices => WorkspaceModules.Forms,
        _ => WorkspaceModules.None
    };
}
