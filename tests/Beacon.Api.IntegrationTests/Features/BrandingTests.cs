using System.Net;
using System.Net.Http.Headers;
using Shouldly;
using Beacon.Api.Features.Branding;
using Beacon.TestInfrastructure;

namespace Beacon.Api.IntegrationTests.Features;

/// <summary>Branding: public read, Global-Admin write, and the Landing page that renders from it.</summary>
[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class BrandingTests(ApiFactory factory)
{
    private const string PublicPath = "/branding";
    private const string AdminPath = $"{ApiCalls.Api}/branding";

    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];

    private readonly HttpClient _admin = factory.ClientFor(TestPersonas.GlobalAdmin);
    private readonly HttpClient _public = factory.ClientFor(null);

    private static MultipartFormDataContent LogoForm(byte[] bytes, string contentType)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, "file", "logo.png" } };
    }

    [Test]
    public async Task Public_read_works_anonymously_with_defaults()
    {
        var branding = await (await _public.GetAsync(PublicPath)).ReadAsync<BrandingResponse>();

        branding.AppName.ShouldNotBeNullOrWhiteSpace();
        branding.ShortBaseUrl.ShouldBe(ApiFactory.ShortBaseUrl);
    }

    [Test]
    public async Task A_global_admin_update_shows_on_the_public_read_and_the_landing_page()
    {
        var key = TestKey.New();
        await _public.GetAsync(PublicPath); // cache the current view first

        var response = await _admin.PutJsonAsync(AdminPath, new UpdateBrandingRequest(key.Name("Links"), "Short and sweet", "https://home.example.com", "#123abc"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var branding = await (await _public.GetAsync(PublicPath)).ReadAsync<BrandingResponse>();
        branding.AppName.ShouldBe(key.Name("Links"));
        branding.Tagline.ShouldBe("Short and sweet");
        branding.HomeUrl.ShouldBe("https://home.example.com");
        branding.PrimaryColor.ShouldBe("#123abc");

        var landing = await (await _public.ResolveAsync(TestKey.New().Code("nope"))).Content.ReadAsStringAsync();
        landing.ShouldContain(key.Name("Links"));
        landing.ShouldContain("https://home.example.com");
    }

    [Test]
    public async Task An_invalid_update_is_rejected()
    {
        var response = await _admin.PutJsonAsync(AdminPath, new UpdateBrandingRequest("Links", null, null, "not-a-colour"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_regular_user_cannot_change_branding()
    {
        var client = factory.ClientFor(TestPersonas.RegularUser);

        (await client.PutJsonAsync(AdminPath, new UpdateBrandingRequest("Hijacked", null, null, null))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.PostAsync($"{AdminPath}/logo", LogoForm(PngBytes, "image/png"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.DeleteAsync($"{AdminPath}/logo")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await (await _public.GetAsync(PublicPath)).ReadAsync<BrandingResponse>()).AppName.ShouldNotBe("Hijacked");
    }

    [Test]
    public async Task Logo_upload_is_served_publicly_and_can_be_cleared()
    {
        (await _admin.PostAsync($"{AdminPath}/logo", LogoForm(PngBytes, "image/png"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var logo = await _public.GetAsync($"{PublicPath}/logo");
        logo.StatusCode.ShouldBe(HttpStatusCode.OK);
        logo.Content.Headers.ContentType?.MediaType.ShouldBe("image/png");
        (await logo.Content.ReadAsByteArrayAsync()).ShouldBe(PngBytes);
        (await (await _public.GetAsync(PublicPath)).ReadAsync<BrandingResponse>()).HasLogo.ShouldBeTrue();

        (await _admin.DeleteAsync($"{AdminPath}/logo")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _public.GetAsync($"{PublicPath}/logo")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_logo_of_the_wrong_type_is_rejected()
    {
        var response = await _admin.PostAsync($"{AdminPath}/logo", LogoForm([1, 2, 3], "application/pdf"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
