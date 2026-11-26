namespace InspectNow.Domain.Organizations;

public sealed class OrganizationRole(long organizationId, string name)
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public long OrganizationId { get; private set; } = organizationId;
    public string Name { get; private set; } = name;
    public string NormalizedName { get; private set; } = name.ToUpperInvariant();
}
