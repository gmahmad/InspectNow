namespace InspectNow.Application.Inspections;

public sealed record InspectionQuestionResult(
    long Id,
    long SourceQuestionId,
    string Text,
    bool IsRequired,
    int Position);