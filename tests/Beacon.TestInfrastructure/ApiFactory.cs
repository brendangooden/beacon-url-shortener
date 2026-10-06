using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TUnit.Core.Interfaces;
using Beacon.Api.Infrastructure.Analytics;
using Beacon.Api.Infrastructure.Persistence;
using ZiggyCreatures.Caching.Fusion;

namespace Beacon.TestInfrastructure;

/// <summary>
/// The API under test, one instance per test class (<c>SharedType.PerClass</c>). Booting it resets
/// the shared database with Respawn, so each class starts clean. Outbound dependencies are swapped
/// for deterministic in-process stand-ins:
/// <list type="bullet">
/// <item>Postgres: the shared <see cref="TestDatabase"/> (real Postgres, never mocked).</item>
/// <item>Redis: not used. FusionCache runs L1-memory-only (no L2, no backplane), so cache
/// invalidation is still exercised but nothing leaves the process.</item>
/// <item>Auth: <see cref="TestAuthenticationHandler"/> is the default scheme.</item>
/// <item>Click analytics: the <see cref="ClickWriter"/> background loop is removed; redirects still
/// enqueue into <see cref="ClickBuffer"/>, which tests drain with <see cref="DrainClicks"/>.</item>
/// </list>
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncInitializer
{
    public const string ShortBaseUrl = "https://beacon.test";

    public async Task InitializeAsync()
    {
        await TestDatabase.EnsureReadyAsync();
        await TestDatabase.ResetAsync();
        _ = Server; // boot the host now, not lazily inside the first test
    }

    /// <summary>A client signed in as <paramref name="persona"/> (or anonymous when null). Never follows redirects.</summary>
    public HttpClient ClientFor(TestPersona? persona)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        if (persona is not null)
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", persona.Token);
        }

        return client;
    }

    /// <summary>Run <paramref name="work"/> against the API's own DbContext (for seeding and for
    /// asserting on state the HTTP surface does not expose).</summary>
    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await work(db);
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> work)
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await work(db);
    }

    /// <summary>Everything the redirect path has queued for the (disabled) click writer.</summary>
    public IReadOnlyList<ClickEvent> DrainClicks()
    {
        var queue = Services.GetRequiredService<ClickBuffer>();
        var drained = new List<ClickEvent>();
        while (queue.Reader.TryRead(out var click))
        {
            drained.Add(click);
        }

        return drained;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:beacon", TestDatabase.ConnectionString);
        builder.UseSetting("ConnectionStrings:cache", string.Empty);
        builder.UseSetting("Aspire:StackExchange:Redis:DisableHealthChecks", "true");
        builder.UseSetting("Aspire:StackExchange:Redis:DisableTracing", "true");
        builder.UseSetting("Auth:GlobalAdmins:Oids:0", TestPersonas.GlobalAdmin.Oid);
        builder.UseSetting("ShortUrl:BaseUrl", ShortBaseUrl);
        builder.UseSetting("Analytics:IpHashSalt", "test-salt");

        builder.ConfigureTestServices(services =>
        {
            // Auth: the test scheme becomes the default authenticate/challenge scheme.
            services.RemoveAll<IClaimsTransformation>();
            services.AddSingleton<IClaimsTransformation, NoOpClaimsTransformation>();
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(o =>
            {
                o.DefaultScheme = TestAuthenticationHandler.SchemeName;
                o.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                o.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
            });

            // Redis L2 + backplane -> none. A memory-only FusionCache keeps the real cache semantics
            // (hits, negative caching, invalidation) with no background L2 writes to race a test,
            // no soft timeouts, and no Redis connection.
            services.RemoveAll<IFusionCache>();
            services.AddSingleton<IFusionCache>(_ => new FusionCache(new FusionCacheOptions
            {
                DefaultEntryOptions = new FusionCacheEntryOptions { Duration = TimeSpan.FromHours(1) },
            }));

            // No background click writer racing the assertions.
            var clickWriter = services.Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(ClickWriter)).ToList();
            foreach (var descriptor in clickWriter)
            {
                services.Remove(descriptor);
            }
        });
    }
}
