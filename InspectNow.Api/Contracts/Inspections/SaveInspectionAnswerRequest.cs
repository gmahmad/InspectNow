using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using InspectNow.Domain.Inspections;

namespace InspectNow.Api.Contracts.Inspections;

public sealed class SaveInspectionAnswerRequest : IValidatableObject
{
    [Required]
    [EnumDataType(typeof(InspectionAnswer))]
    [JsonConverter(typeof(JsonStringEnumConverter<InspectionAnswer>))]
    public InspectionAnswer? Answer { get; init; }

    [StringLength(InspectionQuestion.MaxCommentLength)]
    public string? Comment { get; init; }

    public Guid ExpectedVersion { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ExpectedVersion == Guid.Empty)
            yield return new ValidationResult("The inspection version is required.",
                new[] { nameof(ExpectedVersion) });
    }
}
