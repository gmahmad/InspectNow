namespace InspectNow.Domain.Templates;

public sealed class TemplateQuestion
{
    public const int MaxTextLength = 500;

    public Guid Id { get; private set; }
    public string Text { get; private set; }
    public bool IsRequired { get; private set; }

    internal TemplateQuestion(string text, bool isRequired)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                "Question text is required.",
                nameof(text));
        }

        string trimmedText = text.Trim();

        if (trimmedText.Length > MaxTextLength)
        {
            throw new ArgumentException(
                $"Question text cannot exceed {MaxTextLength} characters.",
                nameof(text));
        }

        Id = Guid.NewGuid();
        Text = trimmedText;
        IsRequired = isRequired;
    }
}