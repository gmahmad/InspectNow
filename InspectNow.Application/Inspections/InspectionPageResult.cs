namespace InspectNow.Application.Inspections;

public sealed record InspectionPageResult(
    IReadOnlyList<InspectionSummaryResult> Items,
    int Page, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}
