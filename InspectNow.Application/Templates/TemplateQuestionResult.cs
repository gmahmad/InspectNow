namespace InspectNow.Application.Templates;

public sealed record TemplateQuestionResult(
    Guid Id,
    string Text,
    bool IsRequired);