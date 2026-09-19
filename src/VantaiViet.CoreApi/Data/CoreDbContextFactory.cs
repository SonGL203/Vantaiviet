using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VantaiViet.CoreApi.Data;

public sealed class CoreDbContextFactory : IDesignTimeDbContextFactory<CoreDbContext>
{
    public CoreDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<CoreDbContextFactory>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Scaffolding and script generation do not open a connection.
        // Port 1 deliberately prevents an unconfigured command reaching a real database.
        var connectionString = configuration.GetConnectionString("CoreDatabase")
            ?? "Host=127.0.0.1;Port=1;Database=configuration_required;Username=unconfigured;Timeout=1";

        var options = new DbContextOptionsBuilder<CoreDbContext>()
            .UseNpgsql(connectionString, postgres =>
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .Options;

        return new CoreDbContext(options);
    }
}
