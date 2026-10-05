namespace InspectNow.Application.Templates;

public enum PublishTemplateOutcome
{
    Published,
    TemplateNotFound,
    TemplateNotDraft,
    NoQuestions,
    ConcurrencyConflict
}