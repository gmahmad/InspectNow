using System.ComponentModel.DataAnnotations;
using InspectNow.Domain.Inspections;

namespace InspectNow.Api.Contracts.Inspections;

public sealed class StartInspectionRequest : IValidatableObject
{
    public Guid TemplateId { get; init; }

    [Required]
    [StringLength(Inspection.MaxSiteNameLength)]
    public string SiteName { get; init; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(
        ValidationContext validationContext)
    {
        if (TemplateId == Guid.Empty)
        {
            yield return new ValidationResult(
                "A template ID is required.",
                new[] { nameof(TemplateId) });
        }
    }
}