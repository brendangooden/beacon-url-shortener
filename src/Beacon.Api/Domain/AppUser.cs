namespace Beacon.Api.Domain;

/// <summary>
/// A lightweight directory row for a person who has signed in. There is no user-management here:
/// the row is upserted from the token claims on each <c>/me</c> call, purely so the app can show a
/// human-readable owner/actor name for an <c>oid</c> (e.g. "owned by Alex Rivera") instead of a guid.
/// </summary>
public sealed class AppUser
{
    private AppUser() { } // EF

    private AppUser(string oid, string email, string name)
    {
        Oid = oid;
        Email = email;
        Name = string.IsNullOrWhiteSpace(name) ? email : name;
        FirstSeenUtc = DateTime.UtcNow;
        LastSeenUtc = FirstSeenUtc;
    }

    public string Oid { get; private set; } = null!; // primary key
    public string Email { get; private set; } = "";
    public string Name { get; private set; } = "";
    public DateTime FirstSeenUtc { get; private set; }
    public DateTime LastSeenUtc { get; private set; }

    public static AppUser Create(string oid, string email, string name) => new(oid, email, name);

    /// <summary>Refresh the display fields from the latest token claims and stamp the visit.</summary>
    public void Seen(string email, string name)
    {
        if (!string.IsNullOrWhiteSpace(email))
        {
            Email = email;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            Name = name;
        }

        LastSeenUtc = DateTime.UtcNow;
    }
}
