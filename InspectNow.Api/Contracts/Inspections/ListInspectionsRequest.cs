using System.ComponentModel.DataAnnotations;
using InspectNow.Application.Inspections;
using InspectNow.Domain.Inspections;

namespace InspectNow.Api.Contracts.Inspections;

public sealed class ListInspectionsRequest : IValidatableObject
{
    [EnumDataType(typeof(InspectionStatus))]
    public InspectionStatus? Status { get; init; }
    [Range(typeof(long), "1", "9223372036854775807")]
    public long? TemplateId { get; init; }
    [StringLength(Inspection.MaxSiteNameLength)]
    public string? SiteName { get; init; }
    public DateTimeOffset? StartedFromUtc { get; init; }
    public DateTimeOffset? StartedToUtc { get; init; }
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 10;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartedFromUtc.HasValue && StartedToUtc.HasValue &&
            StartedFromUtc.Value >= StartedToUtc.Value)
            yield return new ValidationResult(
                "StartedFromUtc must be before StartedToUtc.",
                [nameof(StartedFromUtc), nameof(StartedToUtc)]);
    }

    public InspectionListQuery ToQuery() => new(Status, TemplateId, SiteName,
        StartedFromUtc, StartedToUtc, Page, PageSize);
}
