using System.Net;
using System.Net.Http.Json;
using InspectNow.Api.Contracts.Templates;
using InspectNow.Api.Contracts.Inspections;
using InspectNow.Application.Inspections;
using InspectNow.Domain.Inspections;
using InspectNow.Application.Templates;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace InspectNow.Tests.Integration;

public sealed class TemplateLifecycleTests
{
    [Fact]
    public async Task PublishedTemplate_RejectsAdditionalQuestions()
    {
        // Arrange: start the API against the test database.
        using var factory = new InspectNowApiFactory();

        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                AllowAutoRedirect = false
            });

        await using (var scope =
            factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<InspectNowDbContext>();

            await db.Database.MigrateAsync();
        }

        // Create a unique template for this test run.
        var name = $"Integration test {Guid.NewGuid():N}";

        using var createResponse = await client.PostAsJsonAsync(
            "/api/inspection-templates",
            new { name });

        Assert.Equal(
            HttpStatusCode.Created,
            createResponse.StatusCode);

        var created = await createResponse.Content
            .ReadFromJsonAsync<CreateTemplateResult>();

        Assert.NotNull(created);
        Assert.Equal(name, created!.Name);
        Assert.True(created.Id > 0);
        Assert.NotNull(createResponse.Headers.Location);

        var templateUrl =
            $"/api/inspection-templates/{created.Id}";

        // Add the first question.
        using var addResponse = await client.PostAsJsonAsync(
            $"{templateUrl}/questions",
            new
            {
                text = "Are the floors clean?",
                isRequired = true
            });

        Assert.Equal(
            HttpStatusCode.Created,
            addResponse.StatusCode);

        var added = await addResponse.Content.ReadFromJsonAsync<AddTemplateQuestionResponse>();
        Assert.NotNull(added);
        Assert.True(added.QuestionId > 0);

        // Publish.
        using var publishResponse = await client.PostAsync(
            $"{templateUrl}/publish",
            content: null);

        Assert.Equal(
            HttpStatusCode.NoContent,
            publishResponse.StatusCode);

        // Confirm the persisted state through another request.
        using var getResponse = await client.GetAsync(templateUrl);

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var published = await getResponse.Content
            .ReadFromJsonAsync<TemplateDetailsResult>();

        Assert.NotNull(published);
        Assert.Equal("Published", published!.Status);
        Assert.Single(published.Questions);

        // Attempt an invalid modification after publishing.
        using var rejectedResponse = await client.PostAsJsonAsync(
            $"{templateUrl}/questions",
            new
            {
                text = "This question must not be saved.",
                isRequired = false
            });

        Assert.Equal(
            HttpStatusCode.Conflict,
            rejectedResponse.StatusCode);

        // Verify the rejected request left the data unchanged.
        using var finalResponse = await client.GetAsync(templateUrl);

        Assert.Equal(HttpStatusCode.OK, finalResponse.StatusCode);

        var finalTemplate = await finalResponse.Content
            .ReadFromJsonAsync<TemplateDetailsResult>();

        Assert.NotNull(finalTemplate);
        Assert.Equal("Published", finalTemplate!.Status);

        var question = Assert.Single(finalTemplate.Questions);

        Assert.Equal("Are the floors clean?", question.Text);
        Assert.True(question.IsRequired);
        Assert.Equal(added.QuestionId, question.Id);
    }

    [Fact]
    public async Task NumericIds_PersistSnapshotsAnswersAndConcurrency()
    {
        using var factory = new InspectNowApiFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
        }

        using var create = await client.PostAsJsonAsync("/api/inspection-templates",
            new { name = $"Numeric IDs {Guid.NewGuid():N}" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var template = await create.Content.ReadFromJsonAsync<CreateTemplateResult>();
        Assert.NotNull(template);
        Assert.True(template.Id > 0);

        var templateUrl = $"/api/inspection-templates/{template.Id}";
        using var add = await client.PostAsJsonAsync($"{templateUrl}/questions",
            new { text = "Are the floors clean?", isRequired = true });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);
        var added = await add.Content.ReadFromJsonAsync<AddTemplateQuestionResponse>();
        Assert.NotNull(added);
        Assert.True(added.QuestionId > 0);

        using var publish = await client.PostAsync($"{templateUrl}/publish", null);
        Assert.Equal(HttpStatusCode.NoContent, publish.StatusCode);

        async Task<InspectionDetailsResult> StartAndReadAsync()
        {
            using var start = await client.PostAsJsonAsync("/api/inspections",
                new { templateId = template.Id, siteName = "Lahore Office" });
            Assert.Equal(HttpStatusCode.Created, start.StatusCode);
            Assert.NotNull(start.Headers.Location);
            var started = await start.Content.ReadFromJsonAsync<StartInspectionResponse>();
            Assert.NotNull(started);
            Assert.True(started.Id > 0);
            var details = await client.GetFromJsonAsync<InspectionDetailsResult>(start.Headers.Location);
            Assert.NotNull(details);
            Assert.Equal(started.Id, details.Id);
            Assert.Equal(template.Id, details.TemplateId);
            var question = Assert.Single(details.Questions);
            Assert.True(question.Id > 0);
            Assert.Equal(added.QuestionId, question.SourceQuestionId);
            return details;
        }

        var first = await StartAndReadAsync();
        var second = await StartAndReadAsync();
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Questions[0].Id, second.Questions[0].Id);

        // Exercise the domain and mapping directly; the answer endpoint is the next lesson.
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var firstEdit = await firstDb.Inspections.Include(i => i.Questions)
            .SingleAsync(i => i.Id == first.Id);
        var staleEdit = await secondDb.Inspections.Include(i => i.Questions)
            .SingleAsync(i => i.Id == first.Id);
        var questionId = first.Questions[0].Id;
        firstEdit.AnswerQuestion(questionId, InspectionAnswer.Fail, "Needs cleaning.");
        await firstDb.SaveChangesAsync();
        staleEdit.AnswerQuestion(questionId, InspectionAnswer.Pass, "Stale edit.");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        var saved = await verifyDb.Inspections.Include(i => i.Questions)
            .SingleAsync(i => i.Id == first.Id);
        Assert.Equal(InspectionAnswer.Fail, Assert.Single(saved.Questions).Answer);
        Assert.Equal("Needs cleaning.", saved.Questions[0].Comment);
        Assert.NotEqual(first.Version, saved.Version);

        using var invalidStart = await client.PostAsJsonAsync("/api/inspections",
            new { templateId = 0L, siteName = "Lahore Office" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidStart.StatusCode);
        using var missing = await client.GetAsync($"/api/inspections/{long.MaxValue}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }
}
