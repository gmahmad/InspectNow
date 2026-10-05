using InspectNow.Domain.Templates;

namespace InspectNow.Application.Templates;

public sealed class AddTemplateQuestion
{
    private readonly IInspectionTemplateRepository _repository;

    public AddTemplateQuestion(
        IInspectionTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<AddTemplateQuestionResult> ExecuteAsync(
        Guid templateId,
        string text,
        bool isRequired,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetForUpdateAsync(
            templateId,
            cancellationToken);

        if (template is null)
        {
            return new AddTemplateQuestionResult(
                AddTemplateQuestionOutcome.TemplateNotFound);
        }

        if (template.Status != TemplateStatus.Draft)
        {
            return new AddTemplateQuestionResult(
                AddTemplateQuestionOutcome.TemplateNotDraft);
        }

        var questionId = template.AddQuestion(text, isRequired);

        await _repository.SaveChangesAsync(cancellationToken);

        return new AddTemplateQuestionResult(
            AddTemplateQuestionOutcome.Added,
            questionId);
    }
}