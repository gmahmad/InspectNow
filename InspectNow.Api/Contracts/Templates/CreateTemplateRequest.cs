using System.ComponentModel.DataAnnotations;
using InspectNow.Domain.Templates;

namespace InspectNow.Api.Contracts.Templates;

public sealed class CreateTemplateRequest
{
    [Required(ErrorMessage = "Template name is required.")]
    [StringLength(
        InspectionTemplate.MaxNameLength,
        ErrorMessage = "Template name cannot exceed 200 characters.")]
    public string Name { get; init; } = string.Empty;
}