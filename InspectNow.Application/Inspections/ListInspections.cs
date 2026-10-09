using InspectNow.Domain.Inspections;

namespace InspectNow.Application.Inspections;

public sealed class ListInspections(IInspectionQueries queries)
{
    public Task<InspectionPageResult> ExecuteAsync(
        InspectionListQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Page < 1)
            throw new ArgumentOutOfRangeException(nameof(query.Page));
        if (query.PageSize is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(query.PageSize));
        if (query.TemplateId is <= 0)
            throw new ArgumentOutOfRangeException(nameof(query.TemplateId));
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw new ArgumentOutOfRangeException(nameof(query.Status));
        if (query.SiteName?.Length > Inspection.MaxSiteNameLength)
            throw new ArgumentException("Site name filter is too long.", nameof(query.SiteName));
        if (query.StartedFromUtc.HasValue && query.StartedToUtc.HasValue &&
            query.StartedFromUtc.Value >= query.StartedToUtc.Value)
            throw new ArgumentException("Start of date range must be before its end.");

        cancellationToken.ThrowIfCancellationRequested();
        return queries.ListAsync(query with
        {
            SiteName = string.IsNullOrWhiteSpace(query.SiteName) ? null : query.SiteName.Trim(),
            StartedFromUtc = query.StartedFromUtc?.ToUniversalTime(),
            StartedToUtc = query.StartedToUtc?.ToUniversalTime()
        }, cancellationToken);
    }
}
