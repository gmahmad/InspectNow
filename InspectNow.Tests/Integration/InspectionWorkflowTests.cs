using System.Net;
using System.Net.Http.Json;
using InspectNow.Api.Contracts.Inspections;
using InspectNow.Api.Contracts.Templates;
using InspectNow.Application.Inspections;
using InspectNow.Application.Templates;
using InspectNow.Domain.Inspections;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InspectNow.Tests.Integration;

[Collection("PostgreSQL integration")]
public sealed class InspectionWorkflowTests
{
    [Fact]
    public async Task AnswerAndSubmit_EnforcesValidationVersionsAndFinality()
    {
        using var factory = new InspectNowApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
        }

        using var create = await client.PostAsJsonAsync("/api/inspection-templates",
            new { name = $"Answer workflow {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var template = (await create.Content.ReadFromJsonAsync<CreateTemplateResult>())!;
        var templateUrl = $"/api/inspection-templates/{template.Id}";
        using var addRequired = await client.PostAsJsonAsync($"{templateUrl}/questions",
            new { text = "Floors clean?", isRequired = true });
        Assert.Equal(HttpStatusCode.Created, addRequired.StatusCode);
        using var addOptional = await client.PostAsJsonAsync($"{templateUrl}/questions",
            new { text = "Carpet clean?", isRequired = false });
        Assert.Equal(HttpStatusCode.Created, addOptional.StatusCode);
        using var publish = await client.PostAsync($"{templateUrl}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        async Task<InspectionDetailsResult> StartAsync()
        {
            using var start = await client.PostAsJsonAsync("/api/inspections",
                new { templateId = template.Id, siteName = "Lahore Office" });
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);
            return (await client.GetFromJsonAsync<InspectionDetailsResult>(start.Headers.Location!))!;
        }
        var inspection = await StartAsync();
        var other = await StartAsync();
        var url = $"/api/inspections/{inspection.Id}";
        var required = inspection.Questions.Single(q => q.IsRequired);
        var optional = inspection.Questions.Single(q => !q.IsRequired);
        var answerUrl = $"{url}/questions/{required.Id}/answer";
        var version = inspection.Version;
        Assert.Null(inspection.SubmittedAtUtc);
        Assert.All(inspection.Questions, q => Assert.Null(q.Answer));

        using var missingAnswers = await client.PostAsJsonAsync($"{url}/submit", new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.Conflict, missingAnswers.StatusCode);
        using var missingVersion = await client.PutAsJsonAsync(answerUrl, new { answer = "Pass" });
        Assert.Equal(HttpStatusCode.BadRequest, missingVersion.StatusCode);
        using var missingAnswer = await client.PutAsJsonAsync(answerUrl, new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, missingAnswer.StatusCode);
        using var invalidAnswer = await client.PutAsJsonAsync(answerUrl, new { answer = 99, expectedVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAnswer.StatusCode);
        using var invalidText = await client.PutAsJsonAsync(answerUrl, new { answer = "Unknown", expectedVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, invalidText.StatusCode);
        using var longComment = await client.PutAsJsonAsync(answerUrl,
            new { answer = "Pass", comment = new string('x', 2001), expectedVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, longComment.StatusCode);
        using var notApplicable = await client.PutAsJsonAsync(answerUrl,
            new { answer = "NotApplicable", expectedVersion = version });
        Assert.Equal(HttpStatusCode.BadRequest, notApplicable.StatusCode);
        using var wrongQuestion = await client.PutAsJsonAsync($"{url}/questions/{other.Questions[0].Id}/answer",
            new { answer = "Pass", expectedVersion = version });
        Assert.Equal(HttpStatusCode.NotFound, wrongQuestion.StatusCode);
        using var absent = await client.PutAsJsonAsync($"/api/inspections/{long.MaxValue}/questions/{required.Id}/answer",
            new { answer = "Pass", expectedVersion = version });
        Assert.Equal(HttpStatusCode.NotFound, absent.StatusCode);
        var unchanged = (await client.GetFromJsonAsync<InspectionDetailsResult>(url))!;
        Assert.Equal(version, unchanged.Version);
        Assert.Null(unchanged.Questions.Single(q => q.Id == required.Id).Answer);

        using var saved = await client.PutAsJsonAsync(answerUrl,
            new { answer = "Fail", comment = "  Needs cleaning.  ", expectedVersion = version });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var savedVersion = (await saved.Content.ReadFromJsonAsync<SaveInspectionAnswerResponse>())!.Version;
        Assert.NotEqual(version, savedVersion);
        var afterSave = (await client.GetFromJsonAsync<InspectionDetailsResult>(url))!;
        Assert.Equal("Fail", afterSave.Questions.Single(q => q.Id == required.Id).Answer);
        Assert.Equal("Needs cleaning.", afterSave.Questions.Single(q => q.Id == required.Id).Comment);
        Assert.Equal(savedVersion, afterSave.Version);

        // Replay an old editing revision; ensure both write paths reject it.
        using var staleSave = await client.PutAsJsonAsync(answerUrl,
            new { answer = "Pass", expectedVersion = version });
        Assert.Equal(HttpStatusCode.Conflict, staleSave.StatusCode);
        using var staleSubmit = await client.PostAsJsonAsync($"{url}/submit", new { expectedVersion = version });
        Assert.Equal(HttpStatusCode.Conflict, staleSubmit.StatusCode);

        using var optionalSaved = await client.PutAsJsonAsync($"{url}/questions/{optional.Id}/answer",
            new { answer = "NotApplicable", comment = "No carpet.", expectedVersion = savedVersion });
        Assert.Equal(HttpStatusCode.OK, optionalSaved.StatusCode);
        var latest = (await optionalSaved.Content.ReadFromJsonAsync<SaveInspectionAnswerResponse>())!.Version;
        using var submit = await client.PostAsJsonAsync($"{url}/submit", new { expectedVersion = latest });
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var submitted = (await submit.Content.ReadFromJsonAsync<SubmitInspectionResponse>())!;
        Assert.Equal("Submitted", submitted.Status);
        Assert.NotEqual(latest, submitted.Version);
        Assert.Equal(TimeSpan.Zero, submitted.SubmittedAtUtc.Offset);

        using var locked = await client.PutAsJsonAsync(answerUrl,
            new { answer = "Pass", expectedVersion = submitted.Version });
        Assert.Equal(HttpStatusCode.Conflict, locked.StatusCode);
        using var again = await client.PostAsJsonAsync($"{url}/submit", new { expectedVersion = submitted.Version });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        var final = (await client.GetFromJsonAsync<InspectionDetailsResult>(url))!;
        Assert.Equal("Submitted", final.Status);
        Assert.Equal(submitted.SubmittedAtUtc, final.SubmittedAtUtc!.Value);
        Assert.Equal(submitted.Version, final.Version);
        Assert.Equal("Fail", final.Questions.Single(q => q.Id == required.Id).Answer);
        Assert.Equal("NotApplicable", final.Questions.Single(q => q.Id == optional.Id).Answer);

        // Different contexts: a late answer cannot overwrite a concurrent submission.
        await using var setupScope = factory.Services.CreateAsyncScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var setup = await setupDb.Inspections.Include(i => i.Questions).SingleAsync(i => i.Id == other.Id);
        var requiredId = setup.Questions.Single(q => q.IsRequired).Id;
        setup.AnswerQuestion(requiredId, InspectionAnswer.Pass, null);
        await setupDb.SaveChangesAsync();
        await using var answerScope = factory.Services.CreateAsyncScope();
        await using var submitScope = factory.Services.CreateAsyncScope();
        var answerDb = answerScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var submitDb = submitScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var editing = await answerDb.Inspections.Include(i => i.Questions).SingleAsync(i => i.Id == other.Id);
        var submitting = await submitDb.Inspections.Include(i => i.Questions).SingleAsync(i => i.Id == other.Id);
        submitting.Submit(DateTimeOffset.UtcNow);
        await submitDb.SaveChangesAsync();
        editing.AnswerQuestion(requiredId, InspectionAnswer.Fail, "Late edit");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => answerDb.SaveChangesAsync());
        var protectedResult = (await client.GetFromJsonAsync<InspectionDetailsResult>($"/api/inspections/{other.Id}"))!;
        Assert.Equal("Submitted", protectedResult.Status);
        Assert.Equal("Pass", protectedResult.Questions.Single(q => q.IsRequired).Answer);
    }
}
