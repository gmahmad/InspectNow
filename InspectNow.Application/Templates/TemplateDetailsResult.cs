namespace InspectNow.Application.Templates;

public sealed record TemplateDetailsResult(
    long Id,
    string Name,
    string Status,
    IReadOnlyList<TemplateQuestionResult> Questions);