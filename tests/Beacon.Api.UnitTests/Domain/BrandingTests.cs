using Shouldly;
using Beacon.Api.Common;
using BrandingEntity = Beacon.Api.Domain.Branding;

namespace Beacon.Api.UnitTests.Domain;

public sealed class BrandingTests
{
    [Test]
    public void Update_trims_values_and_blanks_empty_optionals()
    {
        var branding = new BrandingEntity("Beacon");

        branding.Update("  Acme Links ", "  ", " https://acme.test ", "#2563eb");

        branding.AppName.ShouldBe("Acme Links");
        branding.Tagline.ShouldBeNull();
        branding.HomeUrl.ShouldBe("https://acme.test");
        branding.PrimaryColor.ShouldBe("#2563eb");
    }

    [Test]
    [Arguments("", null, null, "app name")]
    [Arguments("Acme", "ftp://acme.test", null, "home URL")]
    [Arguments("Acme", null, "blue", "primary color")]
    [Arguments("Acme", null, "#12345", "primary color")]
    public void Update_rejects_invalid_values(string appName, string? homeUrl, string? color, string reason)
    {
        var branding = new BrandingEntity("Beacon");

        Should.Throw<DomainException>(() => branding.Update(appName, null, homeUrl, color)).Message.ShouldContain(reason);
        branding.AppName.ShouldBe("Beacon");
    }

    [Test]
    public void SetLogo_enforces_non_empty_and_the_size_cap()
    {
        var branding = new BrandingEntity("Beacon");

        Should.Throw<DomainException>(() => branding.SetLogo([], "image/png"));
        Should.Throw<DomainException>(() => branding.SetLogo(new byte[BrandingEntity.MaxLogoBytes + 1], "image/png"));

        branding.SetLogo([1, 2, 3], "image/png");
        branding.HasLogo.ShouldBeTrue();

        branding.ClearLogo();
        branding.HasLogo.ShouldBeFalse();
        branding.LogoContentType.ShouldBeNull();
    }
}
