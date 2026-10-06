using InspectNow.Application.Inspections;
using InspectNow.Domain.Inspections;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Infrastructure.Persistence.Repositories;

public sealed class InspectionRepository : IInspectionRepository
{
    private readonly InspectNowDbContext _dbContext;

    public InspectionRepository(InspectNowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(Inspection inspection)
    {
        _dbContext.Inspections.Add(inspection);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InspectionDetailsResult?> GetDetailsAsync(
    Guid id,
    CancellationToken cancellationToken = default)
    {
        var inspection = await _dbContext.Inspections
            .Where(i => i.Id == id)
            .Select(i => new
            {
                i.Id,
                i.TemplateId,
                i.TemplateName,
                i.SiteName,
                i.Status,
                i.StartedAtUtc,
                i.Version,
                Questions = i.Questions
                    .OrderBy(q => q.Position)
                    .Select(q => new InspectionQuestionResult(
                        q.Id,
                        q.SourceQuestionId,
                        q.Text,
                        q.IsRequired,
                        q.Position))
                    .ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (inspection is null)
        {
            return null;
        }

        return new InspectionDetailsResult(
            inspection.Id,
            inspection.TemplateId,
            inspection.TemplateName,
            inspection.SiteName,
            inspection.Status.ToString(),
            inspection.StartedAtUtc,
            inspection.Version,
            inspection.Questions);
    }
}