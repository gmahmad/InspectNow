namespace InspectNow.Application.Templates;

public sealed record TemplateQuestionResult(
    long Id,
    string Text,
    bool IsRequired);