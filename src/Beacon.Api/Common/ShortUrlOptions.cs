namespace Beacon.Api.Common;

/// <summary>
/// Where ShortCodes resolve. Lives in Common (not Features/Links) because the Links, Admin, and
/// Branding features all build short URLs from it, and features must not import each other.
/// </summary>
public sealed class ShortUrlOptions
{
    public const string SectionName = "ShortUrl";

    /// <summary>Origin the codes resolve on, e.g. https://beacon.example.com (no trailing slash).</summary>
    public string BaseUrl { get; set; } = "http://localhost";
}
