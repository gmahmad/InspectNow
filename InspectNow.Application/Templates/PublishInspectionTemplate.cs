using InspectNow.Domain.Templates;

namespace InspectNow.Application.Templates;

public sealed class PublishInspectionTemplate
{
    private readonly IInspectionTemplateRepository _repository;

    public PublishInspectionTemplate(
        IInspectionTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<PublishTemplateOutcome> ExecuteAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetForUpdateAsync(
            templateId,
            cancellationToken);

        if (template is null)
        {
            return PublishTemplateOutcome.TemplateNotFound;
        }

        if (template.Status != TemplateStatus.Draft)
        {
            return PublishTemplateOutcome.TemplateNotDraft;
        }

        if (template.Questions.Count == 0)
        {
            return PublishTemplateOutcome.NoQuestions;
        }

        template.Publish();

        var saved = await _repository.TrySaveChangesAsync(
            cancellationToken);

        return saved
            ? PublishTemplateOutcome.Published
            : PublishTemplateOutcome.ConcurrencyConflict;
    }
}