namespace InspectNow.Application.Inspections;

public sealed record InspectionDetailsResult(
    long Id,
    long TemplateId,
    string TemplateName,
    string SiteName,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    Guid Version,
    IReadOnlyList<InspectionQuestionResult> Questions);
