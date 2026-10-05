using InspectNow.Application.Templates;
using InspectNow.Domain.Templates;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Infrastructure.Persistence.Repositories;

public sealed class InspectionTemplateRepository
    : IInspectionTemplateRepository
{
    private readonly InspectNowDbContext _dbContext;

    public InspectionTemplateRepository(
        InspectNowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Add(InspectionTemplate template)
    {
        _dbContext.InspectionTemplates.Add(template);
    }

    public async Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TemplateDetailsResult?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.InspectionTemplates
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                t.Status
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (template is null)
        {
            return null;
        }

        return new TemplateDetailsResult(
            template.Id,
            template.Name,
            template.Status.ToString());
    }

    public async Task<InspectionTemplate?> GetForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.InspectionTemplates
            .Include(t => t.Questions)
            .SingleOrDefaultAsync(
                t => t.Id == id,
                cancellationToken);
    }
}