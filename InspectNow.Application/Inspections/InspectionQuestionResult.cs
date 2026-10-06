namespace InspectNow.Application.Inspections;

public sealed record InspectionQuestionResult(
    Guid Id,
    Guid SourceQuestionId,
    string Text,
    bool IsRequired,
    int Position);