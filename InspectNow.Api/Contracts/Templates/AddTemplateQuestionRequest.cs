using System.ComponentModel.DataAnnotations;
using InspectNow.Domain.Templates;

namespace InspectNow.Api.Contracts.Templates;

public sealed class AddTemplateQuestionRequest
{
    [Required(ErrorMessage = "Question text is required.")]
    [StringLength(
        TemplateQuestion.MaxTextLength,
        ErrorMessage = "Question text cannot exceed 500 characters.")]
    public string Text { get; init; } = string.Empty;

    public bool IsRequired { get; init; }
}