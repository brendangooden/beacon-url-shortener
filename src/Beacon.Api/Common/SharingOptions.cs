namespace Beacon.Api.Common;

/// <summary>Sharing policy. When <see cref="AllowedEmailDomain"/> is set (e.g. "example.com"),
/// shares are restricted to <c>name@{domain}</c> recipients and the SPA collects only the local part.
/// Lives in Common (not Features/Sharing) because the Branding feature also exposes the domain.</summary>
public sealed class SharingOptions
{
    public const string SectionName = "Sharing";
    public string? AllowedEmailDomain { get; set; }

    /// <summary>The configured domain, trimmed of a leading '@' and whitespace; null when unrestricted.</summary>
    public string? NormalisedDomain
    {
        get
        {
            var d = AllowedEmailDomain?.Trim().TrimStart('@');
            return string.IsNullOrWhiteSpace(d) ? null : d;
        }
    }
}
