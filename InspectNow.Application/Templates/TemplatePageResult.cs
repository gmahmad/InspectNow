namespace InspectNow.Application.Templates;

public sealed record TemplatePageResult(
    IReadOnlyList<TemplateSummaryResult> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages =>
        (int)Math.Ceiling((double)TotalCount / PageSize);
}