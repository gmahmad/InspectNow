namespace InspectNow.Domain.Forms;

public sealed class FormDefinition(long organizationId, string name, string kind, string fieldsJson)
{
    public long Id { get; private set; }
    public long OrganizationId { get; private set; } = organizationId;
    public string Name { get; private set; } = name;
    public string Kind { get; private set; } = kind;
    public string FieldsJson { get; private set; } = fieldsJson;
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid Version { get; private set; } = Guid.NewGuid();
    public string[] AudienceRoles { get; private set; } = [];
    public Guid? PublicKey { get; private set; }

    public void SetAudience(string[] roles, bool isPublic)
    {
        AudienceRoles = roles;
        PublicKey = isPublic ? PublicKey ?? Guid.NewGuid() : null;
        Version = Guid.NewGuid();
    }

    public void SaveDraft(string name, string kind, string fieldsJson)
    {
        if (PublishedAtUtc is not null) throw new InvalidOperationException("Published forms are read-only.");
        Name = name;
        Kind = kind;
        FieldsJson = fieldsJson;
        Version = Guid.NewGuid();
    }

    public void Publish(DateTimeOffset now)
    {
        PublishedAtUtc = now;
        Version = Guid.NewGuid();
    }
}
