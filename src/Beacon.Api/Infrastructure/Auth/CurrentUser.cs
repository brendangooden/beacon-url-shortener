using System.Security.Claims;
using Microsoft.Extensions.Options;

namespace Beacon.Api.Infrastructure.Auth;

/// <summary>
/// Well-known claim types, read defensively so the app does not depend on one provider's token
/// shape. Providers map their own claims onto these (see the auth provider registrars).
/// </summary>
public static class AppClaimTypes
{
    public const string Oid = "oid";
    public const string ObjectIdentifier = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    public const string Name = "name";
    public const string Groups = "groups";
    public const string GroupSid = "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups";
    public const string PreferredUsername = "preferred_username";
}

/// <summary>
/// The authenticated caller for the current request. Provider-agnostic: it resolves oid/email/name
/// across token shapes and computes Super-Admin from the configured elevation rules.
/// </summary>
public sealed class CurrentUser(IHttpContextAccessor accessor, IOptions<GlobalAdminOptions> globalAdmins)
{
    private readonly GlobalAdminOptions _globalAdmins = globalAdmins.Value;

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string? Oid =>
        Principal?.FindFirstValue(AppClaimTypes.Oid)
        ?? Principal?.FindFirstValue(AppClaimTypes.ObjectIdentifier)
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string Email =>
        Principal?.FindFirstValue(ClaimTypes.Email)
        ?? Principal?.FindFirstValue(AppClaimTypes.PreferredUsername)
        ?? Principal?.FindFirstValue("emails")
        ?? string.Empty;

    public string Name =>
        Principal?.FindFirstValue(AppClaimTypes.Name)
        ?? Principal?.FindFirstValue(ClaimTypes.Name)
        ?? Email;

    /// <summary>True when this user is a Global-Admin (full access to everything).</summary>
    public bool IsGlobalAdmin
    {
        get
        {
            if (Principal is null || !_globalAdmins.HasAnyRule)
            {
                return false;
            }

            return MatchesAny(_globalAdmins.Oids, Oid)
                || MatchesAny(_globalAdmins.Emails, Email)
                || MatchesGroup()
                || MatchesClaim();
        }
    }

    /// <summary>The oid, or throw — for handlers that require an authenticated identity.</summary>
    public string RequireOid() =>
        Oid ?? throw new Common.ForbiddenException("An authenticated identity is required.");

    private bool MatchesGroup()
    {
        if (_globalAdmins.Groups.Count == 0)
        {
            return false;
        }

        var groups = Principal!.FindAll(AppClaimTypes.Groups)
            .Concat(Principal.FindAll(AppClaimTypes.GroupSid))
            .Select(c => c.Value);

        return groups.Any(g => _globalAdmins.Groups.Contains(g, StringComparer.OrdinalIgnoreCase));
    }

    private bool MatchesClaim() =>
        _globalAdmins.Claims.Any(rule =>
            !string.IsNullOrEmpty(rule.Type) &&
            Principal!.FindAll(rule.Type).Any(c => string.Equals(c.Value, rule.Value, StringComparison.OrdinalIgnoreCase)));

    private static bool MatchesAny(IReadOnlyList<string> allowed, string? value) =>
        !string.IsNullOrEmpty(value) &&
        allowed.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
}
