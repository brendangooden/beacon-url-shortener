namespace Beacon.TestInfrastructure;

/// <summary>
/// A signed-in identity for a test request. <see cref="Token"/> goes in the
/// <c>Authorization: Bearer</c> header; <see cref="TestAuthenticationHandler"/> turns it back into
/// the same claims the real providers put on the principal (oid, email, name).
/// </summary>
public sealed record TestPersona(string Oid, string Email, string Name)
{
    public string Token => TestAuthenticationHandler.Encode(this);
}

/// <summary>
/// Pre-baked personas. <see cref="GlobalAdmin"/> is on the factory's <c>Auth:GlobalAdmins:Oids</c>
/// allowlist; every other persona is a plain User. Use <see cref="NewUser"/> when a test needs a
/// distinct person (for example a Share recipient) whose rows no other test touches.
/// </summary>
public static class TestPersonas
{
    public static readonly TestPersona RegularUser = new(
        "11111111-1111-1111-1111-111111111111", "regular.user@test.local", "Regular User");

    public static readonly TestPersona GlobalAdmin = new(
        "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "global.admin@test.local", "Global Admin");

    /// <summary>A fresh plain User with a unique oid and email.</summary>
    public static TestPersona NewUser(string label)
    {
        var id = Guid.NewGuid();
        return new TestPersona(id.ToString(), $"{label}.{id:N}@test.local".ToLowerInvariant(), $"{label} {id.ToString("N")[..8]}");
    }
}
