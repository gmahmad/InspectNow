namespace InspectNow.Application.Templates;

public sealed record TemplateSummaryResult(
    long Id,
    string Name,
    string Status,
    int QuestionCount);