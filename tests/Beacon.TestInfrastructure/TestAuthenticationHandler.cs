using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Beacon.Api.Infrastructure.Auth;

namespace Beacon.TestInfrastructure;

/// <summary>
/// Test authentication scheme. Reads a <see cref="TestPersona"/> token from the bearer header and
/// builds a principal with the claim shape <see cref="CurrentUser"/> reads. No header means no
/// identity, so the API's <c>RequireAuthorization()</c> answers 401 — the anonymous case stays testable.
/// </summary>
public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    private const string BearerPrefix = "Bearer ";
    private const char Separator = '\n';

    public static string Encode(TestPersona persona) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Separator, persona.Oid, persona.Email, persona.Name)));

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string[] parts;
        try
        {
            parts = Encoding.UTF8.GetString(Convert.FromBase64String(header[BearerPrefix.Length..])).Split(Separator);
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed test token."));
        }

        if (parts.Length != 3)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed test token."));
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(AppClaimTypes.Oid, parts[0]),
            new Claim(ClaimTypes.Email, parts[1]),
            new Claim(AppClaimTypes.Name, parts[2]),
        ], SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Claims are pre-baked on the test principal, so any claims transformation is a no-op.</summary>
public sealed class NoOpClaimsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal) => Task.FromResult(principal);
}
