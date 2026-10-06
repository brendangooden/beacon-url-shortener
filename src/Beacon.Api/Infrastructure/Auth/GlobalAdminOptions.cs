namespace Beacon.Api.Infrastructure.Auth;

/// <summary>A single claim (type + value) that grants Global-Admin when present on the principal.</summary>
public sealed class ClaimMatch
{
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// How a Global-Admin is recognised — independent of the identity provider. Global-Admin is one of
/// the only two system roles (the other is a plain User); it grants full access to everything.
/// Any one match elevates. Bound from the "Auth:GlobalAdmins" configuration section.
/// </summary>
public sealed class GlobalAdminOptions
{
    public const string SectionName = "Auth:GlobalAdmins";

    public IReadOnlyList<string> Oids { get; set; } = [];
    public IReadOnlyList<string> Emails { get; set; } = [];
    public IReadOnlyList<string> Groups { get; set; } = [];
    public IReadOnlyList<ClaimMatch> Claims { get; set; } = [];

    public bool HasAnyRule => Oids.Count > 0 || Emails.Count > 0 || Groups.Count > 0 || Claims.Count > 0;
}
