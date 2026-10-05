using InspectNow.Domain.Templates;

namespace InspectNow.Application.Templates;

public sealed class CreateInspectionTemplate
{
    private readonly IInspectionTemplateRepository _repository;

    public CreateInspectionTemplate(
        IInspectionTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<CreateTemplateResult> ExecuteAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var template = new InspectionTemplate(name);

        _repository.Add(template);

        await _repository.SaveChangesAsync(cancellationToken);

        return new CreateTemplateResult(
            template.Id,
            template.Name);
    }
}