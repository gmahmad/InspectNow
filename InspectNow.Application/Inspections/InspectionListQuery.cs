using InspectNow.Domain.Inspections;

namespace InspectNow.Application.Inspections;

public sealed record InspectionListQuery(
    InspectionStatus? Status = null,
    long? TemplateId = null,
    string? SiteName = null,
    DateTimeOffset? StartedFromUtc = null,
    DateTimeOffset? StartedToUtc = null,
    int Page = 1,
    int PageSize = 10);
