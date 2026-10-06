namespace InspectNow.Application.Templates;

public enum AddTemplateQuestionOutcome
{
    Added,
    TemplateNotFound,
    TemplateNotDraft,
    ConcurrencyConflict
}
public sealed record AddTemplateQuestionResult(
    AddTemplateQuestionOutcome Outcome,
    long? QuestionId = null);