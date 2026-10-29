using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace ShiftSync.Infrastructure.Persistence;

public sealed record DatabaseOptions(string Provider);

public static class DatabaseSetup
{
    public static string ResolveProvider(IConfiguration config)
    {
        var named = config["Database:Provider"];
        if (!string.IsNullOrWhiteSpace(named))
            return Normalize(named);

        var connection = config.GetConnectionString("Default") ?? "";
        if (connection.Contains("Host=", StringComparison.OrdinalIgnoreCase)
            && connection.Contains("Username=", StringComparison.OrdinalIgnoreCase))
            return "Postgres";

        if (connection.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
            || connection.Contains("TrustServerCertificate=", StringComparison.OrdinalIgnoreCase))
            return "SqlServer";

        return "Sqlite";
    }

    public static void UseProvider(DbContextOptionsBuilder options, string provider, string connectionString)
    {
        switch (Normalize(provider))
        {
            case "Postgres":
                options.UseNpgsql(connectionString);
                break;
            case "SqlServer":
                options.UseSqlServer(connectionString);
                break;
            default:
                options.UseSqlite(connectionString);
                break;
        }
    }

    public static string Normalize(string provider)
    {
        return provider.Trim().ToLowerInvariant() switch
        {
            "postgres" or "postgresql" or "npgsql" => "Postgres",
            "sqlserver" or "mssql" or "sql server" => "SqlServer",
            _ => "Sqlite"
        };
    }
}
