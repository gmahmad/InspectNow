using InspectNow.Domain.Templates;

namespace InspectNow.Domain.Inspections;

public sealed class Inspection
{
    public const int MaxSiteNameLength = 200;

    private readonly List<InspectionQuestion> _questions = new();

    public long Id { get; private set; }
    public long TemplateId { get; private set; }
    public Guid TemplateVersion { get; private set; }

    public string TemplateName { get; private set; } = string.Empty;
    public string SiteName { get; private set; } = string.Empty;

    public InspectionStatus Status { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public Guid Version { get; private set; }

    public IReadOnlyList<InspectionQuestion> Questions =>
        _questions.AsReadOnly();

    // Used by EF Core when loading saved data.
    private Inspection()
    {
    }

    public static Inspection Start(
        InspectionTemplate template,
        string siteName,
        DateTimeOffset startedAt)
    {
        ArgumentNullException.ThrowIfNull(template);

        if (template.Status != TemplateStatus.Published)
        {
            throw new InvalidOperationException(
                "Inspections can only start from published templates.");
        }

        if (template.Questions.Count == 0)
        {
            throw new InvalidOperationException(
                "The template must contain at least one question.");
        }

        if (string.IsNullOrWhiteSpace(siteName))
        {
            throw new ArgumentException(
                "Site name is required.",
                nameof(siteName));
        }

        var trimmedSiteName = siteName.Trim();

        if (trimmedSiteName.Length > MaxSiteNameLength)
        {
            throw new ArgumentException(
                $"Site name cannot exceed {MaxSiteNameLength} characters.",
                nameof(siteName));
        }

        // Snapshot source identifiers must already be database-generated.
        if (template.Id <= 0 || template.Questions.Any(question => question.Id <= 0))
        {
            throw new InvalidOperationException(
                "Save the template and its questions before starting an inspection.");
        }

        var inspection = new Inspection
        {
            TemplateId = template.Id,
            TemplateVersion = template.Version,
            TemplateName = template.Name,
            SiteName = trimmedSiteName,
            Status = InspectionStatus.Draft,
            StartedAtUtc = startedAt.ToUniversalTime(),
            Version = Guid.NewGuid()
        };

        var position = 1;

        foreach (var question in template.Questions.OrderBy(q => q.Id))
        {
            inspection._questions.Add(
                new InspectionQuestion(
                    question.Id,
                    question.Text,
                    question.IsRequired,
                    position));

            position++;
        }

        return inspection;
    }

    public void AnswerQuestion(
        long questionId,
        InspectionAnswer answer,
        string? comment)
    {
        if (Status != InspectionStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft inspections can be changed.");
        }

        if (questionId <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(questionId), "A saved question ID is required.");
        }

        var question = _questions.SingleOrDefault(
            question => question.Id == questionId);

        if (question is null)
        {
            throw new ArgumentException(
                "The question does not belong to this inspection.",
                nameof(questionId));
        }

        question.RecordAnswer(answer, comment);

        Version = Guid.NewGuid();
    }
}