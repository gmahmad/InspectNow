namespace InspectNow.Application.Inspections;

public sealed record InspectionSummaryResult(
    long Id, long TemplateId, string TemplateName, string SiteName,
    string Status, DateTimeOffset StartedAtUtc, DateTimeOffset? SubmittedAtUtc,
    int QuestionCount, int AnsweredQuestionCount);
