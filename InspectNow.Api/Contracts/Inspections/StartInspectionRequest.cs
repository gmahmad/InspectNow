using System.ComponentModel.DataAnnotations;
using InspectNow.Domain.Inspections;

namespace InspectNow.Api.Contracts.Inspections;

public sealed class StartInspectionRequest : IValidatableObject
{
    public long TemplateId { get; init; }

    [Required]
    [StringLength(Inspection.MaxSiteNameLength)]
    public string SiteName { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (TemplateId <= 0)
        {
            yield return new ValidationResult(
                "A template ID is required.",
                new[] { nameof(TemplateId) });
        }
    }
}