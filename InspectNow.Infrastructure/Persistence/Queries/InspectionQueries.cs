using InspectNow.Application.Inspections;
using Microsoft.EntityFrameworkCore;

namespace InspectNow.Infrastructure.Persistence.Queries;

public sealed class InspectionQueries(InspectNowDbContext dbContext) : IInspectionQueries
{
    public async Task<InspectionPageResult> ListAsync(
        InspectionListQuery filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Inspections.AsNoTracking().AsQueryable();
        if (filter.Status.HasValue)
            query = query.Where(i => i.Status == filter.Status.Value);
        if (filter.TemplateId.HasValue)
            query = query.Where(i => i.TemplateId == filter.TemplateId.Value);
        if (filter.SiteName is not null)
        {
            // Treat %, _ and backslash literally, rather than as SQL LIKE wildcards.
            var literal = filter.SiteName.Replace("\\", "\\\\")
                .Replace("%", "\\%").Replace("_", "\\_");
            var pattern = $"%{literal}%";
            query = query.Where(i => EF.Functions.ILike(i.SiteName, pattern, "\\"));
        }
        if (filter.StartedFromUtc.HasValue)
            query = query.Where(i => i.StartedAtUtc >= filter.StartedFromUtc.Value);
        if (filter.StartedToUtc.HasValue)
            query = query.Where(i => i.StartedAtUtc < filter.StartedToUtc.Value);

        // Sequential awaits: both queries share this scoped DbContext.
        var totalCount = await query.CountAsync(cancellationToken);
        var offset = ((long)filter.Page - 1) * filter.PageSize;
        if (offset >= totalCount)
            return new InspectionPageResult([], filter.Page, filter.PageSize, totalCount);

        var rows = await query.OrderByDescending(i => i.StartedAtUtc)
            .ThenByDescending(i => i.Id)
            .Skip((int)offset).Take(filter.PageSize)
            .Select(i => new
            {
                i.Id, i.TemplateId, i.TemplateName, i.SiteName, i.Status,
                i.StartedAtUtc, i.SubmittedAtUtc,
                QuestionCount = i.Questions.Count(),
                AnsweredQuestionCount = i.Questions.Count(q => q.Answer != null)
            }).ToListAsync(cancellationToken);

        var items = rows.Select(i => new InspectionSummaryResult(
            i.Id, i.TemplateId, i.TemplateName, i.SiteName, i.Status.ToString(),
            i.StartedAtUtc, i.SubmittedAtUtc, i.QuestionCount, i.AnsweredQuestionCount)).ToList();
        return new InspectionPageResult(items, filter.Page, filter.PageSize, totalCount);
    }
}
