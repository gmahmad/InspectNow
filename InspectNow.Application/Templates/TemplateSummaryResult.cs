namespace InspectNow.Application.Templates;

public sealed record TemplateSummaryResult(
    Guid Id,
    string Name,
    string Status,
    int QuestionCount);