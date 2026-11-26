namespace InspectNow.Domain.Organizations;

public sealed class Site(long organizationId, string name)
{
    public long Id { get; private set; }
    public long OrganizationId { get; private set; } = organizationId;
    public string Name { get; private set; } = name;
}
