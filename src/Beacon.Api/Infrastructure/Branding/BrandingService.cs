using System.Net;
using Microsoft.EntityFrameworkCore;
using Beacon.Api.Infrastructure.Persistence;
using ZiggyCreatures.Caching.Fusion;
using BrandingEntity = Beacon.Api.Domain.Branding;

namespace Beacon.Api.Infrastructure.Branding;

/// <summary>Lightweight, cacheable branding (no logo bytes) for the public surface.</summary>
public sealed record BrandingView(
    string AppName, string? Tagline, string? HomeUrl, string? PrimaryColor, bool HasLogo, long Version);

public sealed record LogoContent(byte[] Bytes, string ContentType);

/// <summary>
/// Reads and mutates the single global <see cref="BrandingEntity"/> row. The view is cached in
/// FusionCache (the public not-found page can be hit by bots); logo bytes are read from the DB.
/// </summary>
public sealed class BrandingService(AppDbContext db, IFusionCache cache)
{
    public const string DefaultAppName = "Beacon";
    private const string CacheKey = "branding:view";

    public ValueTask<BrandingView> GetViewAsync(CancellationToken ct = default) =>
        cache.GetOrSetAsync<BrandingView>(
            CacheKey,
            async (_, token) => ToView(await EnsureAsync(token)),
            token: ct);

    public async Task<LogoContent?> GetLogoAsync(CancellationToken ct)
    {
        var row = await db.Branding
            .Where(b => b.Id == BrandingEntity.SingletonId)
            .Select(b => new { b.Logo, b.LogoContentType })
            .FirstOrDefaultAsync(ct);

        return row?.Logo is { Length: > 0 } bytes && row.LogoContentType is { } ct2
            ? new LogoContent(bytes, ct2)
            : null;
    }

    public async Task<BrandingView> UpdateAsync(string appName, string? tagline, string? homeUrl, string? primaryColor, CancellationToken ct)
    {
        var row = await LoadTrackedAsync(ct);
        row.Update(appName, tagline, homeUrl, primaryColor);
        await SaveAndInvalidateAsync(ct);
        return ToView(row);
    }

    public async Task SetLogoAsync(byte[] bytes, string contentType, CancellationToken ct)
    {
        var row = await LoadTrackedAsync(ct);
        row.SetLogo(bytes, contentType);
        await SaveAndInvalidateAsync(ct);
    }

    public async Task ClearLogoAsync(CancellationToken ct)
    {
        var row = await LoadTrackedAsync(ct);
        row.ClearLogo();
        await SaveAndInvalidateAsync(ct);
    }

    private async Task<BrandingEntity> EnsureAsync(CancellationToken ct)
    {
        var row = await db.Branding.FirstOrDefaultAsync(b => b.Id == BrandingEntity.SingletonId, ct);
        if (row is not null)
        {
            return row;
        }

        row = new BrandingEntity(DefaultAppName);
        db.Branding.Add(row);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request created it first — reload.
            db.ChangeTracker.Clear();
            row = await db.Branding.FirstAsync(b => b.Id == BrandingEntity.SingletonId, ct);
        }

        return row;
    }

    private async Task<BrandingEntity> LoadTrackedAsync(CancellationToken ct)
    {
        await EnsureAsync(ct);
        return await db.Branding.AsTracking().FirstAsync(b => b.Id == BrandingEntity.SingletonId, ct);
    }

    private async Task SaveAndInvalidateAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        await cache.RemoveAsync(CacheKey, token: ct);
    }

    private static BrandingView ToView(BrandingEntity b) =>
        new(b.AppName, b.Tagline, b.HomeUrl, b.PrimaryColor, b.HasLogo, b.UpdatedOnUtc.Ticks);

    /// <summary>Render the branded not-found landing page (self-contained HTML, light + dark).</summary>
    public static string RenderNotFound(BrandingView b)
    {
        var name = WebUtility.HtmlEncode(b.AppName);
        var accent = string.IsNullOrWhiteSpace(b.PrimaryColor) ? "#2563eb" : b.PrimaryColor;
        var logo = b.HasLogo
            ? $"<img class=\"logo\" src=\"/branding/logo?v={b.Version}\" alt=\"{name}\" />"
            : $"<div class=\"wordmark\">{name}</div>";
        var tagline = string.IsNullOrWhiteSpace(b.Tagline)
            ? string.Empty
            : $"<p class=\"tagline\">{WebUtility.HtmlEncode(b.Tagline)}</p>";
        var home = string.IsNullOrWhiteSpace(b.HomeUrl)
            ? string.Empty
            : $"<a class=\"btn\" href=\"{WebUtility.HtmlEncode(b.HomeUrl)}\">Go to homepage</a>";

        return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<meta name="robots" content="noindex" />
<title>Link not found · {{name}}</title>
<style>
  :root { --accent: {{accent}}; --bg:#f8fafc; --fg:#0f172a; --muted:#64748b; --card:#ffffff; --border:#e2e8f0; }
  @media (prefers-color-scheme: dark) {
    :root { --bg:#0b1120; --fg:#e2e8f0; --muted:#94a3b8; --card:#111827; --border:#1f2937; }
  }
  * { box-sizing: border-box; }
  body { margin:0; min-height:100dvh; display:grid; place-items:center; padding:24px;
    font-family: ui-sans-serif, system-ui, -apple-system, Segoe UI, Roboto, Helvetica, Arial, sans-serif;
    background: radial-gradient(1200px 600px at 50% -10%, color-mix(in srgb, var(--accent) 14%, var(--bg)), var(--bg));
    color: var(--fg); }
  .card { width:100%; max-width:460px; text-align:center; background:var(--card);
    border:1px solid var(--border); border-radius:20px; padding:40px 32px;
    box-shadow: 0 12px 40px -12px rgba(0,0,0,.25); }
  .logo { max-height:56px; max-width:200px; object-fit:contain; }
  .wordmark { font-size:24px; font-weight:700; letter-spacing:-.02em; color:var(--accent); }
  .code { margin:20px 0 4px; font-size:56px; font-weight:800; line-height:1; letter-spacing:-.04em;
    background:linear-gradient(180deg, var(--fg), var(--muted)); -webkit-background-clip:text;
    background-clip:text; color:transparent; }
  h1 { margin:8px 0 6px; font-size:20px; font-weight:650; }
  p { margin:0; color:var(--muted); font-size:15px; line-height:1.5; }
  .tagline { margin-top:14px; font-size:13px; }
  .btn { display:inline-block; margin-top:24px; padding:11px 20px; border-radius:12px;
    background:var(--accent); color:#fff; text-decoration:none; font-weight:600; font-size:14px; }
  .btn:hover { filter:brightness(1.05); }
  footer { margin-top:22px; font-size:12px; color:var(--muted); }
</style>
</head>
<body>
  <main class="card">
    {{logo}}
    <div class="code">404</div>
    <h1>This link doesn’t exist</h1>
    <p>The short link you followed is invalid, disabled, or has expired.</p>
    {{tagline}}
    {{home}}
    <footer>Powered by {{name}}</footer>
  </main>
</body>
</html>
""";
    }
}

public static class BrandingServiceExtensions
{
    public static IServiceCollection AddBranding(this IServiceCollection services) =>
        services.AddScoped<BrandingService>();
}
