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
    protected override void ConfigureWebHost(
        IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: false)
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