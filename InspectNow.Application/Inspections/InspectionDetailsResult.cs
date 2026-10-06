namespace InspectNow.Application.Inspections;

public sealed record InspectionDetailsResult(
    Guid Id,
    Guid TemplateId,
    string TemplateName,
    string SiteName,
    string Status,
    DateTimeOffset StartedAtUtc,
    Guid Version,
    IReadOnlyList<InspectionQuestionResult> Questions);