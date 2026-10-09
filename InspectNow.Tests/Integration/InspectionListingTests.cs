using System.Net;
using System.Net.Http.Json;
using InspectNow.Application.Inspections;
using InspectNow.Domain.Inspections;
using InspectNow.Domain.Templates;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InspectNow.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InspectionListingTests
{
    [Fact]
    public async Task List_FiltersAndPagesInPostgres_WithStableOrderAndLiteralSearch()
    {
        using var factory = new InspectNowApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        long templateId, firstId, secondId, thirdId;
        var time = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var template = new InspectionTemplate($"List test {Guid.NewGuid():N}");
            template.AddQuestion("Clean?", true);
            template.AddQuestion("Carpet?", false);
            template.Publish();
            db.InspectionTemplates.Add(template);
            await db.SaveChangesAsync();
            templateId = template.Id;

            var first = Inspection.Start(template, @"Office 100%_A\West", time);
            var second = Inspection.Start(template, @"Office 100XYA\West", time.AddHours(1));
            var third = Inspection.Start(template, "Karachi OFFICE", time.AddHours(1));
            // Save separately so the Id tie-break expectation does not rely on insert batching order.
            db.Inspections.Add(first);
            await db.SaveChangesAsync();
            db.Inspections.Add(second);
            await db.SaveChangesAsync();
            db.Inspections.Add(third);
            await db.SaveChangesAsync();
            firstId = first.Id; secondId = second.Id; thirdId = third.Id;
            first.AnswerQuestion(first.Questions.Single(q => q.IsRequired).Id, InspectionAnswer.Pass, null);
            first.Submit(time.AddHours(3));
            await db.SaveChangesAsync();
        }

        var url = $"/api/inspections?templateId={templateId}";
        async Task<InspectionPageResult> GetAsync(string query) =>
            (await client.GetFromJsonAsync<InspectionPageResult>(url + query))!;

        var page1 = await GetAsync("&pageSize=2&siteName=office");
        Assert.Equal(3, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Equal(new[] { thirdId, secondId }, page1.Items.Select(i => i.Id).ToArray());
        var page2 = await GetAsync("&page=2&pageSize=2");
        var submitted = Assert.Single(page2.Items);
        Assert.Equal(firstId, submitted.Id);
        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal(2, submitted.QuestionCount);
        Assert.Equal(1, submitted.AnsweredQuestionCount);
        Assert.NotNull(submitted.SubmittedAtUtc);

        var drafts = await GetAsync("&status=Draft");
        Assert.Equal(2, drafts.TotalCount);
        Assert.All(drafts.Items, i => Assert.Equal("Draft", i.Status));
        var completed = await GetAsync("&status=Submitted");
        Assert.Equal(firstId, Assert.Single(completed.Items).Id);
        var literal = await GetAsync("&siteName=" + Uri.EscapeDataString(@"100%_A\West"));
        Assert.Equal(firstId, Assert.Single(literal.Items).Id);

        // From is inclusive; To is exclusive. Offsets are normalized before Npgsql.
        var dates = await GetAsync("&startedFromUtc=" +
            Uri.EscapeDataString("2026-10-01T05:00:00+05:00") +
            "&startedToUtc=2026-10-01T01:00:00Z");
        Assert.Equal(firstId, Assert.Single(dates.Items).Id);
        var later = await GetAsync("&startedFromUtc=2026-10-01T01:00:00Z");
        Assert.Equal(2, later.TotalCount);

        var beyond = await GetAsync("&page=2147483647&pageSize=100");
        Assert.Empty(beyond.Items);
        Assert.Equal(3, beyond.TotalCount);
        var absent = (await client.GetFromJsonAsync<InspectionPageResult>(
            $"/api/inspections?templateId={long.MaxValue}"))!;
        Assert.Empty(absent.Items);
        Assert.Equal(0, absent.TotalPages);
        using var healthy = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
    }
}
