namespace InspectNow.Application.Templates;

public enum AddTemplateQuestionOutcome
{
    Added,
    TemplateNotFound,
    TemplateNotDraft
}

public sealed record AddTemplateQuestionResult(
    AddTemplateQuestionOutcome Outcome,
    Guid? QuestionId = null);