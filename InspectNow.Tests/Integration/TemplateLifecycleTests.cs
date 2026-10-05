using System.Net;
using System.Net.Http.Json;
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
    }
}