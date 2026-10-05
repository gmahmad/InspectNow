using InspectNow.Domain.Templates;

namespace InspectNow.Application.Templates;

public interface IInspectionTemplateRepository
{
    void Add(InspectionTemplate template);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task<TemplateDetailsResult?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<InspectionTemplate?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}