namespace InspectNow.Application.Templates;

public sealed record CreateTemplateResult(
    Guid Id,
    string Name);