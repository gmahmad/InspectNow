namespace InspectNow.Application.Templates;

public sealed class GetInspectionTemplate
{
    private readonly IInspectionTemplateRepository _repository;

    public GetInspectionTemplate(
        IInspectionTemplateRepository repository)
    {
        _repository = repository;
    }

    public Task<TemplateDetailsResult?> ExecuteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetDetailsAsync(id, cancellationToken);
    }
}