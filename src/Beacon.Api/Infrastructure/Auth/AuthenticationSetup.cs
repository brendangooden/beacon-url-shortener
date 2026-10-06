namespace Beacon.Api.Infrastructure.Auth;

/// <summary>
/// Wires authentication from a registry of pluggable <see cref="IAuthProvider"/>s. Which providers
/// are enabled comes from config ("Auth:Providers", first = default scheme); when unset it infers
/// Entra if an app registration is present, else the Dev provider so the POC runs today.
/// </summary>
public static class AuthenticationSetup
{
    private const string ProvidersKey = "Auth:Providers";

    public static IServiceCollection AddAppAuthentication(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        services.AddHttpContextAccessor();
        services.Configure<GlobalAdminOptions>(config.GetSection(GlobalAdminOptions.SectionName));
        services.Configure<DevAuthOptions>(config.GetSection(DevAuthOptions.SectionName));
        services.AddScoped<CurrentUser>();
        services.AddScoped<ResourceAccess>();

        // The provider registry. Add a new IdP by adding one entry here.
        var registry = new IAuthProvider[] { new EntraAuthProvider(), new DevAuthProvider() }
            .ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);

        var enabled = ResolveEnabled(config);
        var defaultScheme = registry[enabled[0]].SchemeName;

        var auth = services.AddAuthentication(defaultScheme);
        foreach (var key in enabled)
        {
            if (!registry.TryGetValue(key, out var provider))
            {
                throw new InvalidOperationException($"Unknown auth provider '{key}'. Known: {string.Join(", ", registry.Keys)}.");
            }

            provider.Register(auth, config, env);
        }

        services.AddAuthorization();
        return services;
    }

    private static string[] ResolveEnabled(IConfiguration config)
    {
        var configured = config.GetSection(ProvidersKey).Get<string[]>();
        if (configured is { Length: > 0 })
        {
            return configured;
        }

        var entraConfigured = !string.IsNullOrWhiteSpace(config[$"{EntraAuthProvider.ConfigSection}:ClientId"]);
        return entraConfigured ? [new EntraAuthProvider().Key] : [new DevAuthProvider().Key];
    }
}
