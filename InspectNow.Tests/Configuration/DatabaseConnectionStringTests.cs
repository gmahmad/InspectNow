using InspectNow.Api.Configuration;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace InspectNow.Tests.Configuration;

public sealed class DatabaseConnectionStringTests
{
    [Fact]
    public void WithoutPasswordFile_PreservesExistingConnectionString()
    {
        const string existing = "Host=localhost;Database=inspectnow_tests;Username=test_user;Password=local_example";
        var configuration = Config(existing);
        Assert.Equal(existing, DatabaseConnectionString.Resolve(configuration));
    }

    [Fact]
    public void PasswordFile_OverridesPassword_AndHandlesEditorNewline()
    {
        var path = Path.GetTempFileName();
        try
        {
            // Semicolons must be escaped by the connection-string builder.
            File.WriteAllText(path, " example;password \r\n");
            var resolved = DatabaseConnectionString.Resolve(Config(
                "Host=db;Database=inspectnow;Username=inspectnow_app;Password=old", path));
            var settings = new NpgsqlConnectionStringBuilder(resolved);
            Assert.Equal(" example;password ", settings.Password);
            Assert.Equal("db", settings.Host);
            Assert.Equal("inspectnow", settings.Database);
            Assert.Equal("inspectnow_app", settings.Username);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void EmptyPasswordFile_FailsAtStartup()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, " \r\n");
            Assert.Throws<InvalidOperationException>(() => DatabaseConnectionString.Resolve(
                Config("Host=db;Database=inspectnow", path)));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MissingConnectionString_FailsAtStartup()
    {
        Assert.Throws<InvalidOperationException>(() =>
            DatabaseConnectionString.Resolve(Config(null)));
    }

    private static IConfiguration Config(string? connection, string? passwordFile = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:InspectNow"] = connection,
            ["Database:PasswordFile"] = passwordFile
        }).Build();
}
