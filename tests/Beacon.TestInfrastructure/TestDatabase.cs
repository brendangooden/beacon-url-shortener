using Microsoft.EntityFrameworkCore;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.TestInfrastructure;

/// <summary>
/// The one Postgres the whole test run shares. A throwaway Testcontainers instance (dies with the
/// run), or the database CI hands us via <c>ConnectionStrings__Postgres</c>. Migrated exactly once;
/// <see cref="ResetAsync"/> wipes the <c>public</c> schema between test classes with Respawn but
/// keeps <c>__EFMigrationsHistory</c>, so every class starts on a migrated, empty database.
/// </summary>
public static class TestDatabase
{
    private const string CiConnectionStringVariable = "ConnectionStrings__Postgres";
    private const string MigrationsHistoryTable = "__EFMigrationsHistory";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static Respawner? _respawner;
    private static string? _connectionString;

    public static string ConnectionString =>
        _connectionString ?? throw new InvalidOperationException("Call EnsureReadyAsync before reading the connection string.");

    public static async Task EnsureReadyAsync()
    {
        if (_respawner is not null)
        {
            return;
        }

        await Gate.WaitAsync();
        try
        {
            if (_respawner is not null)
            {
                return;
            }

            _connectionString = Environment.GetEnvironmentVariable(CiConnectionStringVariable);
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                // No explicit dispose: the Testcontainers reaper removes it when the test process exits.
                var container = new PostgreSqlBuilder("postgres:17-alpine").Build();
                await container.StartAsync();
                _connectionString = container.GetConnectionString();
            }

            await MigrateAsync(_connectionString);

            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();
            _respawner = await Respawner.CreateAsync(connection, new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("public", MigrationsHistoryTable)],
            });
        }
        finally
        {
            Gate.Release();
        }
    }

    public static async Task ResetAsync()
    {
        var respawner = _respawner ?? throw new InvalidOperationException("Call EnsureReadyAsync before resetting.");
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await respawner.ResetAsync(connection);
    }

    private static async Task MigrateAsync(string connectionString)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString);
        AppDbContext.ConfigureOptions(builder);
        await using var db = new AppDbContext(builder.Options);
        await db.Database.MigrateAsync();
    }
}
