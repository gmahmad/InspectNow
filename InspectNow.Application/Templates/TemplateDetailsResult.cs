namespace InspectNow.Application.Templates;

public sealed record TemplateDetailsResult(
    Guid Id,
    string Name,
    string Status,
    IReadOnlyList<TemplateQuestionResult> Questions);