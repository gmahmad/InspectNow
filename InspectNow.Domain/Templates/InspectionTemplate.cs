namespace InspectNow.Domain.Templates;

public sealed class InspectionTemplate
{
    public const int MaxNameLength = 200;

    public long Id { get; private set; }
    public string Name { get; private set; }
    public TemplateStatus Status { get; private set; }

    private readonly List<TemplateQuestion> _questions = new();

    public IReadOnlyList<TemplateQuestion> Questions =>
        _questions.AsReadOnly();

    public Guid Version { get; private set; } = Guid.NewGuid();

    public InspectionTemplate(string name)
    {
        Name = ValidateName(name);

        Status = TemplateStatus.Draft;
    }

    public void Rename(string name)
    {
        EnsureDraft();

        Name = ValidateName(name);
        Version = Guid.NewGuid();
    }

    public TemplateQuestion AddQuestion(string text, bool isRequired)
    {
        EnsureDraft();

        var question = new TemplateQuestion(text, isRequired);

        _questions.Add(question);
        Version = Guid.NewGuid();

        return question;
    }

    public void Publish()
    {
        EnsureDraft();

        if (_questions.Count == 0)
        {
            throw new InvalidOperationException(
                "A template must contain at least one question before publishing.");
        }

        Status = TemplateStatus.Published;
        Version = Guid.NewGuid();
    }

    private void EnsureDraft()
    {
        if (Status != TemplateStatus.Draft)
        {
            throw new InvalidOperationException(
                "Only draft templates can be modified.");
        }
    }

    private static string ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Template name is required.",
                nameof(name));
        }

        string trimmedName = name.Trim();

        if (trimmedName.Length > MaxNameLength)
        {
            throw new ArgumentException(
                $"Template name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return trimmedName;
    }
}