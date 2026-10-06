using System.Security.Cryptography;

namespace Beacon.Api.Common;

/// <summary>
/// ShortCode alphabet, generation, and validation. Random base62 (length 7) by default, plus
/// custom/vanity codes validated against the same charset. Reserved prefixes can never be a code
/// (see ADR-0001) so a code can never shadow a real route (/api, /admin, /health, ...).
/// </summary>
public static class ShortCodes
{
    public const int DefaultLength = 7;
    public const int MinVanityLength = 3;
    public const int MaxLength = 64;

    // Case-sensitive base62. The Code column uses a case-sensitive index to preserve all 62 symbols.
    public const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    /// <summary>Route prefixes served by the single origin. A code equal to one of these is rejected.</summary>
    public static readonly IReadOnlySet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "api", "admin", "health", "alive", "assets", "static", "public", "branding",
        "favicon.ico", "robots.txt", "sitemap.xml", "openapi", "swagger", "_",
    };

    /// <summary>One random base62 candidate of the given length. Uniqueness is checked by the caller against the DB.</summary>
    public static string NewCandidate(int length = DefaultLength)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++)
        {
            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }

    /// <summary>True when every character is in the base62 alphabet.</summary>
    public static bool IsWellFormed(string code) =>
        !string.IsNullOrEmpty(code) && code.All(c => Alphabet.Contains(c, StringComparison.Ordinal));

    public static bool IsReserved(string code) => Reserved.Contains(code);

    /// <summary>Validate a caller-supplied vanity code; throws <see cref="DomainException"/> when invalid.</summary>
    public static void ValidateVanity(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("A custom code cannot be empty.");
        }

        if (code.Length is < MinVanityLength or > MaxLength)
        {
            throw new DomainException($"A custom code must be {MinVanityLength}-{MaxLength} characters.");
        }

        if (!IsWellFormed(code))
        {
            throw new DomainException("A custom code may use only letters and digits (A-Z, a-z, 0-9).");
        }

        if (IsReserved(code))
        {
            throw new DomainException($"'{code}' is reserved and cannot be used as a code.");
        }
    }
}
