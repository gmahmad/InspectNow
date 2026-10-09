namespace InspectNow.Application.Inspections;

// Read-only DTO queries are separate from the aggregate write repository.
public interface IInspectionQueries
{
    Task<InspectionPageResult> ListAsync(
        InspectionListQuery query, CancellationToken cancellationToken = default);
}
