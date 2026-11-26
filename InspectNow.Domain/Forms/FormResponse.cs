namespace InspectNow.Domain.Forms;

public sealed class FormResponse(long organizationId, long formId, string issuer, string subject)
{
    public long Id { get; private set; }
    public long OrganizationId { get; private set; } = organizationId;
    public long FormId { get; private set; } = formId;
    public string Issuer { get; private set; } = issuer;
    public string Subject { get; private set; } = subject;
    public string AnswersJson { get; private set; } = "{}";
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid Version { get; private set; }

    public void Save(string answersJson, DateTimeOffset now, bool submit)
    {
        if (SubmittedAtUtc is not null) throw new InvalidOperationException("Submitted responses are read-only.");
        AnswersJson = answersJson;
        UpdatedAtUtc = now;
        SubmittedAtUtc = submit ? now : null;
        Version = Guid.NewGuid();
    }
}
