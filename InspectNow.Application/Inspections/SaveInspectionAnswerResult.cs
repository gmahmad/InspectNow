namespace InspectNow.Application.Inspections;

public enum SaveInspectionAnswerOutcome
{
    Saved,
    InspectionNotFound,
    QuestionNotFound,
    InspectionNotDraft,
    ConcurrencyConflict,
    InvalidAnswer
}

public sealed record SaveInspectionAnswerResult(
    SaveInspectionAnswerOutcome Outcome,
    Guid? Version = null,
    string? Error = null);
