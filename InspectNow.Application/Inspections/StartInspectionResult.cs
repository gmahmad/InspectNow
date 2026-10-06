namespace InspectNow.Application.Inspections;

public enum StartInspectionOutcome
{
    Started,
    TemplateNotFound,
    TemplateNotPublished
}

public sealed record StartInspectionResult(
    StartInspectionOutcome Outcome,
    long? InspectionId = null);