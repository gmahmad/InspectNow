using InspectNow.Domain.Templates;

namespace InspectNow.Application.Templates;

public interface IInspectionTemplateRepository
{
    void Add(InspectionTemplate template);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<TemplateDetailsResult?> GetDetailsAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<InspectionTemplate?> GetForUpdateAsync(
        long id,
        CancellationToken cancellationToken = default);

    Task<bool> TrySaveChangesAsync(
    CancellationToken cancellationToken = default);

    Task<TemplatePageResult> ListAsync(
    TemplateStatus? status,
    int page,
    int pageSize,
    CancellationToken cancellationToken = default);
}