using System.ComponentModel.DataAnnotations;

namespace InspectNow.Api.Contracts.Inspections;

public sealed class SubmitInspectionRequest : IValidatableObject
{
    public Guid ExpectedVersion { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpectedVersion == Guid.Empty)
            yield return new ValidationResult("The inspection version is required.",
                new[] { nameof(ExpectedVersion) });
    }
}
