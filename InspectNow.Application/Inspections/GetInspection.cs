namespace InspectNow.Application.Inspections;

public sealed class GetInspection
{
    private readonly IInspectionRepository _repository;

    public GetInspection(IInspectionRepository repository)
    {
        _repository = repository;
    }

    public Task<InspectionDetailsResult?> ExecuteAsync(
        long id,
        CancellationToken cancellationToken = default)
    {
        return _repository.GetDetailsAsync(id, cancellationToken);
    }
}