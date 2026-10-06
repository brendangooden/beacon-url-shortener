using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Beacon.Api.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef` build the context without the Aspire host supplying a connection string.
/// EF only needs the provider + model to scaffold; it never connects, so a placeholder is fine.
/// </summary>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>();
        options.UseNpgsql("Host=localhost;Database=beacon"); // placeholder, never connects
        AppDbContext.ConfigureOptions(options);
        return new AppDbContext(options.Options);
    }
}
