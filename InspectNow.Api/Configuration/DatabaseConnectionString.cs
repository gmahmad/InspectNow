using Npgsql;

namespace InspectNow.Api.Configuration;

public static class DatabaseConnectionString
{
    public static string Resolve(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("InspectNow");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Connection string 'InspectNow' is missing.");

        var passwordFile = configuration["Database:PasswordFile"];
        if (string.IsNullOrWhiteSpace(passwordFile))
            return connectionString; // Existing Visual Studio User Secrets workflow.

        // Compose mounts the secret at runtime; it never enters the image or YAML.
        // Remove the newline a text editor may append, but preserve password spaces.
        var password = File.ReadAllText(passwordFile).TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("The database password file is empty.");

        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Password = password
        };
        return settings.ConnectionString;
    }
}
