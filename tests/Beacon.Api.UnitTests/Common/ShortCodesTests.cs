using Shouldly;
using Beacon.Api.Common;

namespace Beacon.Api.UnitTests.Common;

public sealed class ShortCodesTests
{
    [Test]
    [Arguments("abc")]
    [Arguments("ABCxyz0189")]
    [Arguments("Z")]
    public void IsWellFormed_accepts_base62(string code)
    {
        ShortCodes.IsWellFormed(code).ShouldBeTrue();
    }

    [Test]
    [Arguments("")]
    [Arguments("has-dash")]
    [Arguments("has space")]
    [Arguments("under_score")]
    [Arguments("slash/es")]
    [Arguments("ümlaut")]
    public void IsWellFormed_rejects_anything_outside_base62(string code)
    {
        ShortCodes.IsWellFormed(code).ShouldBeFalse();
    }

    [Test]
    [Arguments("api")]
    [Arguments("API")]
    [Arguments("Admin")]
    [Arguments("health")]
    [Arguments("branding")]
    public void IsReserved_matches_route_prefixes_case_insensitively(string code)
    {
        ShortCodes.IsReserved(code).ShouldBeTrue();
    }

    [Test]
    public void IsReserved_is_false_for_an_ordinary_code()
    {
        ShortCodes.IsReserved("launch2026").ShouldBeFalse();
    }

    [Test]
    public void ValidateVanity_accepts_a_valid_code()
    {
        Should.NotThrow(() => ShortCodes.ValidateVanity("Spring26"));
    }

    [Test]
    [Arguments("", "empty")]
    [Arguments("   ", "empty")]
    [Arguments("ab", "3-64 characters")]
    [Arguments("bad-code", "only letters and digits")]
    [Arguments("admin", "reserved")]
    public void ValidateVanity_rejects_invalid_codes_with_a_reason(string code, string reason)
    {
        var ex = Should.Throw<DomainException>(() => ShortCodes.ValidateVanity(code));
        ex.Message.ShouldContain(reason);
    }

    [Test]
    public void ValidateVanity_rejects_a_code_over_the_max_length()
    {
        var tooLong = new string('a', ShortCodes.MaxLength + 1);

        Should.Throw<DomainException>(() => ShortCodes.ValidateVanity(tooLong)).Message.ShouldContain("3-64 characters");
        Should.NotThrow(() => ShortCodes.ValidateVanity(new string('a', ShortCodes.MaxLength)));
    }

    [Test]
    public void NewCandidate_is_well_formed_default_length_and_varies()
    {
        var candidates = Enumerable.Range(0, 200).Select(_ => ShortCodes.NewCandidate()).ToList();

        candidates.ShouldAllBe(c => c.Length == ShortCodes.DefaultLength && ShortCodes.IsWellFormed(c));
        // 62^7 possibilities: 200 draws colliding would mean the generator is broken, not unlucky.
        candidates.Distinct(StringComparer.Ordinal).Count().ShouldBe(candidates.Count);
    }

    [Test]
    public void NewCandidate_honours_a_custom_length()
    {
        ShortCodes.NewCandidate(12).Length.ShouldBe(12);
    }
}
