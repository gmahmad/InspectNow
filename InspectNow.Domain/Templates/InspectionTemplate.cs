namespace InspectNow.Domain.Templates;

public sealed class InspectionTemplate
{
    public const int MaxNameLength = 200;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public TemplateStatus Status { get; private set; }

    private readonly List<TemplateQuestion> _questions = new();

    public IReadOnlyList<TemplateQuestion> Questions =>
        _questions.AsReadOnly();
    public InspectionTemplate(string name)
    {
        Name = ValidateName(name);
        Id = Guid.NewGuid();
        Status = TemplateStatus.Draft;
    }

    public void Rename(string name)
    {
        EnsureDraft();
        Name = ValidateName(name);
    }

    public Guid AddQuestion(string text, bool isRequired)
    {
        EnsureDraft();

        var question = new TemplateQuestion(text, isRequired);
        _questions.Add(question);

        return question.Id;
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