using InspectNow.Domain.Templates;

namespace InspectNow.Application.Templates;

public sealed class ListInspectionTemplates
{
    private readonly IInspectionTemplateRepository _repository;

    public ListInspectionTemplates(
        IInspectionTemplateRepository repository)
    {
        _repository = repository;
    }

    public Task<TemplatePageResult> ExecuteAsync(
        TemplateStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (pageSize < 1 || pageSize > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize));
        }

        if (status.HasValue &&
            !Enum.IsDefined(typeof(TemplateStatus), status.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        return _repository.ListAsync(
            status,
            page,
            pageSize,
            cancellationToken);
    }
}