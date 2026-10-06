using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Beacon.Api.Infrastructure.Auth;

/// <summary>
/// Identity used by the <see cref="DevAuthHandler"/>. Comes from X-Dev-* request headers when
/// present, else these configured defaults. Enabled by listing the "Dev" provider (see
/// <see cref="AuthenticationSetup"/>); it must not be enabled in a hardened deployment.
/// </summary>
public sealed class DevAuthOptions
{
    public const string SectionName = "Auth:Dev";
    public const string Scheme = "Dev";

    public string DefaultOid { get; init; } = "00000000-0000-0000-0000-000000000001";
    public string DefaultEmail { get; init; } = "dev@localhost";
    public string DefaultName { get; init; } = "Local Dev";
}

public sealed class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptions<DevAuthOptions> devOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    // Development / POC auth. Authenticates every request as a staff identity so the whole
    // permission model works before a real IdP app registration exists.
    private const string OidHeader = "X-Dev-Oid";
    private const string EmailHeader = "X-Dev-Email";
    private const string NameHeader = "X-Dev-Name";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var dev = devOptions.Value;

        var oid = Header(OidHeader) ?? dev.DefaultOid;
        var email = Header(EmailHeader) ?? dev.DefaultEmail;
        var name = Header(NameHeader) ?? dev.DefaultName;

        var claims = new[]
        {
            new Claim(AppClaimTypes.Oid, oid),
            new Claim(ClaimTypes.Email, email),
            new Claim(AppClaimTypes.Name, name),
        };

        var identity = new ClaimsIdentity(claims, DevAuthOptions.Scheme);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), DevAuthOptions.Scheme);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private string? Header(string name) =>
        Request.Headers.TryGetValue(name, out var v) && !string.IsNullOrWhiteSpace(v) ? v.ToString() : null;
}
