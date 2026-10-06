using InspectNow.Domain.Inspections;

namespace InspectNow.Application.Inspections;

public interface IInspectionRepository
{
    void Add(Inspection inspection);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<InspectionDetailsResult?> GetDetailsAsync(
    Guid id,
    CancellationToken cancellationToken = default);
}