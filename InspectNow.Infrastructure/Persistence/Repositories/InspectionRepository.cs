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

    public void Add(Inspection inspection) => _dbContext.Inspections.Add(inspection);

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TrySaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // The caller returns immediately. Never retry with this tracked state.
            return false;
        }
    }

    public Task<Inspection?> GetForUpdateAsync(
        long id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Inspections
            .Include(inspection => inspection.Questions)
            .SingleOrDefaultAsync(inspection => inspection.Id == id, cancellationToken);
    }

    public async Task<InspectionDetailsResult?> GetDetailsAsync(
        long id, CancellationToken cancellationToken = default)
    {
        var inspection = await _dbContext.Inspections
            .Where(inspection => inspection.Id == id)
            .Select(inspection => new
            {
                inspection.Id,
                inspection.TemplateId,
                inspection.TemplateName,
                inspection.SiteName,
                inspection.Status,
                inspection.StartedAtUtc,
                inspection.SubmittedAtUtc,
                inspection.Version,
                Questions = inspection.Questions.OrderBy(question => question.Position)
                    .Select(question => new
                    {
                        question.Id,
                        question.SourceQuestionId,
                        question.Text,
                        question.IsRequired,
                        question.Position,
                        question.Answer,
                        question.Comment
                    }).ToList()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (inspection is null)
            return null;

        // Convert enums to response strings after SQL has materialized the projection.
        var questions = inspection.Questions.Select(question => new InspectionQuestionResult(
            question.Id, question.SourceQuestionId, question.Text, question.IsRequired,
            question.Position, question.Answer?.ToString(), question.Comment)).ToList();

        return new InspectionDetailsResult(
            inspection.Id, inspection.TemplateId, inspection.TemplateName,
            inspection.SiteName, inspection.Status.ToString(), inspection.StartedAtUtc,
            inspection.SubmittedAtUtc, inspection.Version, questions);
    }
}
