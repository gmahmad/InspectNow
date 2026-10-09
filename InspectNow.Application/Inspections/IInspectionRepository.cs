using InspectNow.Domain.Inspections;

namespace InspectNow.Application.Inspections;

public interface IInspectionRepository
{
    void Add(Inspection inspection);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default);
    Task<Inspection?> GetForUpdateAsync(long id, CancellationToken cancellationToken = default);
    Task<InspectionDetailsResult?> GetDetailsAsync(long id, CancellationToken cancellationToken = default);
}
