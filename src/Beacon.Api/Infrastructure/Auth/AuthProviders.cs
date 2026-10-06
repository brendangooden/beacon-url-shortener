using Microsoft.AspNetCore.Authentication;
using Microsoft.Identity.Web;

namespace Beacon.Api.Infrastructure.Auth;

/// <summary>
/// A pluggable identity-provider registrar. Each provider owns one authentication scheme and knows
/// how to wire it. Adding a new IdP (Auth0, Okta, Google, ...) means adding one implementation and
/// listing its <see cref="Key"/> in the provider registry — no other code changes.
/// </summary>
public interface IAuthProvider
{
    /// <summary>Config key used to enable this provider, e.g. "Entra" or "Dev".</summary>
    string Key { get; }

    /// <summary>The authentication scheme this provider registers.</summary>
    string SchemeName { get; }

    void Register(AuthenticationBuilder auth, IConfiguration config, IHostEnvironment env);
}

/// <summary>Microsoft Entra ID (Azure AD) JWT bearer, via Microsoft.Identity.Web. Reads the "AzureAd" section.</summary>
public sealed class EntraAuthProvider : IAuthProvider
{
    public const string Scheme = "EntraBearer";
    public const string ConfigSection = "AzureAd";

    public string Key => "Entra";
    public string SchemeName => Scheme;

    public void Register(AuthenticationBuilder auth, IConfiguration config, IHostEnvironment env) =>
        auth.AddMicrosoftIdentityWebApi(
            config,
            configSectionName: ConfigSection,
            jwtBearerScheme: Scheme,
            subscribeToJwtBearerMiddlewareDiagnosticsEvents: false);
}

/// <summary>
/// Development / POC provider: authenticates every request as a staff identity so the whole
/// permission model works before a real IdP app registration exists. Must not be enabled in a
/// hardened production deployment.
/// </summary>
public sealed class DevAuthProvider : IAuthProvider
{
    public string Key => "Dev";
    public string SchemeName => DevAuthOptions.Scheme;

    public void Register(AuthenticationBuilder auth, IConfiguration config, IHostEnvironment env) =>
        auth.AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthOptions.Scheme, _ => { });
}
