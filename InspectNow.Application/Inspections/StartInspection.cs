using InspectNow.Application.Templates;
using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;

namespace InspectNow.Application.Inspections;

public sealed class StartInspection
{
    private readonly IInspectionTemplateRepository _templates;
    private readonly IInspectionRepository _inspections;
    private readonly TimeProvider _timeProvider;

    public StartInspection(
        IInspectionTemplateRepository templates,
        IInspectionRepository inspections,
        TimeProvider timeProvider)
    {
        _templates = templates;
        _inspections = inspections;
        _timeProvider = timeProvider;
    }

    public async Task<StartInspectionResult> ExecuteAsync(
        Guid templateId,
        string siteName,
        CancellationToken cancellationToken = default)
    {
        var template = await _templates.GetForUpdateAsync(
            templateId,
            cancellationToken);

        if (template is null)
        {
            return new StartInspectionResult(
                StartInspectionOutcome.TemplateNotFound);
        }

        if (template.Status != TemplateStatus.Published)
        {
            return new StartInspectionResult(
                StartInspectionOutcome.TemplateNotPublished);
        }

        var inspection = Inspection.Start(
            template,
            siteName,
            _timeProvider.GetUtcNow());

        _inspections.Add(inspection);

        await _inspections.SaveChangesAsync(cancellationToken);

        return new StartInspectionResult(
            StartInspectionOutcome.Started,
            inspection.Id);
    }
}