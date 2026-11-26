namespace InspectNow.Domain.Organizations;

public sealed class Organization
{
    public const int MaxNameLength = 200;

    public long Id { get; private set; }
    public string Name { get; private set; }
    public OrganizationType Type { get; private set; }

    public Organization(string name, OrganizationType type = OrganizationType.General)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var trimmedName = name.Trim();
        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Organization name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        Name = trimmedName;
        Type = type;
    }
}
