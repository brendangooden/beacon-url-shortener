using System.Text;
using Beacon.Api.Common;
using Beacon.Api.Infrastructure.Analytics;
using Beacon.Api.Infrastructure.Branding;
using Beacon.Api.Infrastructure.Caching;

namespace Beacon.Api.Features.Redirect;

/// <summary>
/// The public redirect hot-path: GET /{code} -> 302 to the Destination. Resolves through
/// FusionCache, records the Click without blocking, and serves a branded 404 landing page for
/// unknown/disabled/expired codes (and any other unmatched non-API path via the fallback).
/// </summary>
public static class RedirectEndpoints
{
    public static void MapRedirectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/{code}", HandleAsync)
            .AllowAnonymous()
            .ExcludeFromDescription()
            .WithName("Redirect");

        // Any other unmatched path (multi-segment, root) also gets the branded landing — except
        // /api/*, which keeps the JSON ProblemDetails 404 the SPA expects.
        app.MapFallback(async (HttpContext http, BrandingService branding, CancellationToken ct) =>
        {
            if (http.Request.Path.StartsWithSegments("/api"))
            {
                return Results.NotFound();
            }

            return await BrandedNotFoundAsync(branding, ct);
        }).AllowAnonymous().ExcludeFromDescription();
    }

    private static async Task<IResult> HandleAsync(
        string code,
        HttpContext http,
        LinkCache cache,
        ClickBuffer clicks,
        BrandingService branding,
        CancellationToken ct)
    {
        // A code can never be a reserved route word or contain non-base62 chars.
        if (!ShortCodes.IsWellFormed(code) || ShortCodes.IsReserved(code))
        {
            return await BrandedNotFoundAsync(branding, ct);
        }

        var entry = await cache.GetAsync(code, ct);
        if (entry is null || !Resolvable(entry))
        {
            return await BrandedNotFoundAsync(branding, ct);
        }

        clicks.TryEnqueue(BuildClickEvent(entry.LinkId, code, http));
        return Results.Redirect(entry.Destination, permanent: false);
    }

    private static async Task<IResult> BrandedNotFoundAsync(BrandingService branding, CancellationToken ct)
    {
        var view = await branding.GetViewAsync(ct);
        var html = BrandingService.RenderNotFound(view);
        return Results.Content(html, "text/html; charset=utf-8", Encoding.UTF8, StatusCodes.Status404NotFound);
    }

    private static bool Resolvable(LinkCacheEntry entry) =>
        entry.IsActive && (entry.ExpiresOnUtc is null || entry.ExpiresOnUtc > DateTime.UtcNow);

    private static ClickEvent BuildClickEvent(Guid linkId, string code, HttpContext http)
    {
        var headers = http.Request.Headers;
        return new ClickEvent(
            linkId,
            code,
            DateTime.UtcNow,
            Referrer: NullIfEmpty(headers.Referer.ToString()),
            UserAgent: NullIfEmpty(headers.UserAgent.ToString()),
            IpAddress: ClientIp(http),
            Country: NullIfEmpty(headers["CF-IPCountry"].ToString()) ?? NullIfEmpty(headers["X-Forwarded-Country"].ToString()));
    }

    private static string? ClientIp(HttpContext http)
    {
        var forwarded = http.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return http.Connection.RemoteIpAddress?.ToString();
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
