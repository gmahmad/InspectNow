namespace InspectNow.Api.Contracts.Inspections;

public sealed record SubmitInspectionResponse(
    long Id, string Status, DateTimeOffset SubmittedAtUtc, Guid Version);
