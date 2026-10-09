using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using InspectNow.Application.Inspections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace InspectNow.Tests.Integration;

// These pipeline tests need no running PostgreSQL and never change a database.
public sealed class ApiFoundationTests
{
    [Theory]
    [InlineData("/missing-route?secret=never-echo", 404)]
    [InlineData("/api/inspections?page=0", 400)]
    [InlineData("/api/inspections?pageSize=101", 400)]
    [InlineData("/api/inspections?status=999", 400)]
    [InlineData("/api/inspections?templateId=0", 400)]
    [InlineData("/api/inspections?startedFromUtc=2026-10-02T00:00:00Z&startedToUtc=2026-10-01T00:00:00Z", 400)]
    public async Task InvalidRequests_ReturnProblemDetailsAndMatchingTrace(string url, int status)
    {
        using var factory = new FoundationFactory();
        using var client = CreateClient(factory);
        using var response = await client.GetAsync(url);
        Assert.Equal(status, (int)response.StatusCode);
        await AssertProblemAsync(response, status);
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    public async Task UnexpectedError_IsSafeAndCorrelated(string accept)
    {
        using var factory = new FoundationFactory();
        using var host = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IInspectionQueries>();
            services.AddScoped<IInspectionQueries, ThrowingQueries>();
        }));
        using var client = CreateClient(host);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        using var response = await client.GetAsync("/api/inspections");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-database-detail", body);
        Assert.DoesNotContain("stackTrace", body);
        await AssertProblemAsync(response, 500);
    }

    [Fact]
    public async Task HealthChecks_LiveWithoutDatabase_ReadyReportsUnavailable()
    {
        using var factory = new FoundationFactory();
        using var client = CreateClient(factory);
        using var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal("Healthy", await live.Content.ReadAsStringAsync());
        using var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal("Unhealthy", await ready.Content.ReadAsStringAsync());
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, int status)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(status, json.GetProperty("status").GetInt32());
        var traceId = json.GetProperty("traceId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(traceId));
        Assert.Equal(response.Headers.GetValues("X-Trace-Id").Single(), traceId);
        Assert.DoesNotContain("?", json.GetProperty("instance").GetString()!);
    }

    private sealed class FoundationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // A deliberately closed local port makes the readiness failure deterministic.
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:InspectNow",
                "Host=127.0.0.1;Port=1;Database=inspectnow_tests;Username=unused;Password=unused;Timeout=1;Pooling=false");
        }
    }

    public sealed class ThrowingQueries : IInspectionQueries
    {
        public Task<InspectionPageResult> ListAsync(
            InspectionListQuery query, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("secret-database-detail");
    }
}
