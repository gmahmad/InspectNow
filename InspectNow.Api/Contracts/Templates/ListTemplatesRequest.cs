using System.ComponentModel.DataAnnotations;
using InspectNow.Domain.Templates;

namespace InspectNow.Api.Contracts.Templates;

public sealed class ListTemplatesRequest
{
    [EnumDataType(typeof(TemplateStatus))]
    public TemplateStatus? Status { get; init; }

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 10;
}