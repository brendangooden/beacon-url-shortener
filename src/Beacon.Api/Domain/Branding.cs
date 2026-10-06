using System.Text.RegularExpressions;
using Beacon.Api.Common;

namespace Beacon.Api.Domain;

/// <summary>
/// Global branding for the public surface (the branded not-found landing page). A single row.
/// The logo image is stored inline; keep it small.
/// </summary>
public sealed partial class Branding
{
    public static readonly Guid SingletonId = Guid.Parse("b0000000-0000-0000-0000-000000000001");
    public const int MaxLogoBytes = 512 * 1024;

    private Branding() { } // EF

    public Branding(string appName)
    {
        Id = SingletonId;
        AppName = appName;
        UpdatedOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string AppName { get; private set; } = null!;
    public string? Tagline { get; private set; }
    public string? HomeUrl { get; private set; }
    public string? PrimaryColor { get; private set; }
    public string? LogoContentType { get; private set; }
    public byte[]? Logo { get; private set; }
    public DateTime UpdatedOnUtc { get; private set; }

    public bool HasLogo => Logo is { Length: > 0 };

    public void Update(string appName, string? tagline, string? homeUrl, string? primaryColor)
    {
        if (string.IsNullOrWhiteSpace(appName))
        {
            throw new DomainException("An app name is required.");
        }

        if (!string.IsNullOrWhiteSpace(homeUrl) &&
            (!Uri.TryCreate(homeUrl, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            throw new DomainException("The home URL must be an absolute http(s) URL.");
        }

        if (!string.IsNullOrWhiteSpace(primaryColor) && !HexColor().IsMatch(primaryColor))
        {
            throw new DomainException("The primary color must be a hex value like #2563eb.");
        }

        AppName = appName.Trim();
        Tagline = Blank(tagline);
        HomeUrl = Blank(homeUrl);
        PrimaryColor = Blank(primaryColor);
        Touch();
    }

    public void SetLogo(byte[] bytes, string contentType)
    {
        if (bytes.Length == 0)
        {
            throw new DomainException("The logo file is empty.");
        }

        if (bytes.Length > MaxLogoBytes)
        {
            throw new DomainException($"The logo must be {MaxLogoBytes / 1024} KB or smaller.");
        }

        Logo = bytes;
        LogoContentType = contentType;
        Touch();
    }

    public void ClearLogo()
    {
        Logo = null;
        LogoContentType = null;
        Touch();
    }

    private void Touch() => UpdatedOnUtc = DateTime.UtcNow;

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();
}
