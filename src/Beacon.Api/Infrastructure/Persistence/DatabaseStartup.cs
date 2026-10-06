using Microsoft.EntityFrameworkCore;

namespace Beacon.Api.Infrastructure.Persistence;

public static partial class DatabaseStartup
{
    /// <summary>
    /// Apply EF Core migrations at startup. Fine for a POC; a hardened deploy runs migrations as a
    /// separate gated step. MigrateAsync only applies migrations that exist in the assembly.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count == 0)
        {
            LogUpToDate(logger);
            return;
        }

        LogApplying(logger, pending.Count);
        await db.Database.MigrateAsync();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Database is up to date; no migrations to apply.")]
    private static partial void LogUpToDate(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applying {Count} database migration(s).")]
    private static partial void LogApplying(ILogger logger, int count);
}
