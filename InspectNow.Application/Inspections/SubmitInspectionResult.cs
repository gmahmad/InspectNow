namespace InspectNow.Application.Inspections;

public enum SubmitInspectionOutcome
{
    Submitted,
    InspectionNotFound,
    InspectionNotDraft,
    RequiredAnswersMissing,
    ConcurrencyConflict,
    InvalidVersion
}

public sealed record SubmitInspectionResult(
    SubmitInspectionOutcome Outcome,
    Guid? Version = null,
    DateTimeOffset? SubmittedAtUtc = null);
