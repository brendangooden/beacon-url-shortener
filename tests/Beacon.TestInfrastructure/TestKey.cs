namespace Beacon.TestInfrastructure;

/// <summary>
/// A per-test unique key. Stamp it on every row a test creates (names, vanity codes, titles) and
/// filter by it before asserting, so seed rows and other tests' rows never inflate a count.
/// The value is base62-safe, so it can also start a vanity ShortCode.
/// </summary>
public sealed class TestKey
{
    private TestKey(string value) => Value = value;

    /// <summary>"t" + 32 hex chars: 33 chars, well inside the 64-char ShortCode limit.</summary>
    public string Value { get; }

    public static TestKey New() => new($"t{Guid.NewGuid():N}");

    /// <summary>A vanity ShortCode unique to this test, e.g. <c>Code("a")</c>.</summary>
    public string Code(string suffix) => Value + suffix;

    public string Name(string suffix) => $"{Value} {suffix}";

    public override string ToString() => Value;
}
