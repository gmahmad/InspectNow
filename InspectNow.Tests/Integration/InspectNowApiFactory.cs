using System.Net.Http.Headers;
using InspectNow.Api.Authorization;
using InspectNow.Domain.Organizations;
using InspectNow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace InspectNow.Tests.Integration;

public sealed class InspectNowApiFactory
    : WebApplicationFactory<Program>
{
    public static InspectNowDbContext GetMemberDb(IServiceProvider services, HttpClient client)
    {
        var value = client.DefaultRequestHeaders.GetValues(OrganizationAccess.Header).Single();
        services.GetRequiredService<OrganizationScope>().Select(
            long.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
        return services.GetRequiredService<InspectNowDbContext>();
    }

    public async Task<HttpClient> CreateMemberClientAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<InspectNowDbContext>();
        await db.Database.MigrateAsync();
        var organization = new Organization($"Test organization {Guid.NewGuid():N}");
        db.Organizations.Add(organization);
        await db.SaveChangesAsync();

        var subject = Guid.NewGuid().ToString("N");
        db.OrganizationMemberships.Add(new OrganizationMembership(organization.Id, TestTokens.Issuer, subject));
        await db.SaveChangesAsync();

        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.Issue(subject));
        client.DefaultRequestHeaders.Add(OrganizationAccess.Header, organization.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return client;
    }

    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString("InspectNowTests");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'InspectNowTests' is missing.");
        }

        var connection = new NpgsqlConnectionStringBuilder(
            connectionString);

        if (connection.Database != "inspectnow_tests")
        {
            throw new InvalidOperationException(
                "Integration tests must use inspectnow_tests.");
        }

        builder.ConfigureTestServices(services =>
        {
            TestTokens.Configure(services);
            services.RemoveAll<InspectNowDbContext>();

            services.RemoveAll<
                DbContextOptions<InspectNowDbContext>>();

            services.RemoveAll<
                IDbContextOptionsConfiguration<InspectNowDbContext>>();

            services.AddDbContext<InspectNowDbContext>(options =>
                options.UseNpgsql(connectionString));
        });
    }
}
