using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Branding;

namespace Beacon.Api.Features.Branding;

public sealed record UpdateBrandingRequest(
    [property: Required, StringLength(100)] string AppName,
    [property: StringLength(200)] string? Tagline,
    [property: StringLength(2048)] string? HomeUrl,
    [property: StringLength(9)] string? PrimaryColor);

public sealed record BrandingResponse(
    string AppName, string? Tagline, string? HomeUrl, string? PrimaryColor, bool HasLogo, string? LogoUrl,
    string ShortBaseUrl, string? ShareEmailDomain, long Version);

public static class BrandingEndpoints
{
    private static readonly HashSet<string> AllowedLogoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/svg+xml", "image/webp", "image/gif", "image/x-icon",
    };

    /// <summary>Public branding (read) — consumed by the SPA and the branded not-found page.</summary>
    public static void MapPublicBrandingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/branding", async (BrandingService svc, IOptions<ShortUrlOptions> shortUrl, IOptions<SharingOptions> sharing, CancellationToken ct) =>
            Results.Ok(ToResponse(await svc.GetViewAsync(ct), shortUrl.Value, sharing.Value))).AllowAnonymous().WithTags("Branding");

        app.MapGet("/branding/logo", async (BrandingService svc, CancellationToken ct) =>
        {
            var logo = await svc.GetLogoAsync(ct);
            return logo is null ? Results.NotFound() : Results.File(logo.Bytes, logo.ContentType);
        }).AllowAnonymous().WithTags("Branding");
    }

    /// <summary>Super-Admin branding management (mapped under the authenticated /api/v1 group).</summary>
    public static void MapBrandingAdminEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/branding").WithTags("Branding");

        group.MapPut("/", async (UpdateBrandingRequest req, BrandingService svc, CurrentUser user, IOptions<ShortUrlOptions> shortUrl, IOptions<SharingOptions> sharing, CancellationToken ct) =>
        {
            RequireGlobalAdmin(user);
            var view = await svc.UpdateAsync(req.AppName, req.Tagline, req.HomeUrl, req.PrimaryColor, ct);
            return Results.Ok(ToResponse(view, shortUrl.Value, sharing.Value));
        });

        group.MapPost("/logo", async (IFormFile file, BrandingService svc, CurrentUser user, CancellationToken ct) =>
        {
            RequireGlobalAdmin(user);
            if (file is null || file.Length == 0)
            {
                throw new DomainException("No logo file was provided.");
            }

            if (!AllowedLogoTypes.Contains(file.ContentType))
            {
                throw new DomainException("The logo must be a PNG, JPEG, SVG, WebP, GIF, or ICO image.");
            }

            if (file.Length > Domain.Branding.MaxLogoBytes)
            {
                throw new DomainException($"The logo must be {Domain.Branding.MaxLogoBytes / 1024} KB or smaller.");
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct);
            await svc.SetLogoAsync(ms.ToArray(), file.ContentType, ct);
            return Results.NoContent();
        }).DisableAntiforgery();

        group.MapDelete("/logo", async (BrandingService svc, CurrentUser user, CancellationToken ct) =>
        {
            RequireGlobalAdmin(user);
            await svc.ClearLogoAsync(ct);
            return Results.NoContent();
        });
    }

    private static void RequireGlobalAdmin(CurrentUser user)
    {
        if (!user.IsGlobalAdmin)
        {
            throw new ForbiddenException("Only a global admin can change branding.");
        }
    }

    private static BrandingResponse ToResponse(BrandingView v, ShortUrlOptions shortUrl, SharingOptions sharing) =>
        new(v.AppName, v.Tagline, v.HomeUrl, v.PrimaryColor, v.HasLogo,
            v.HasLogo ? $"/branding/logo?v={v.Version}" : null, shortUrl.BaseUrl.TrimEnd('/'), sharing.NormalisedDomain, v.Version);
}
