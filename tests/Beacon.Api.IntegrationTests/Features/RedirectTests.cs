using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

/// <summary>
/// The public <c>GET /{code}</c> hot path. Always GET: the redirect route is GET-only and the
/// fallback catches every other method (HEAD included) with the Landing page.
/// </summary>
[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class RedirectTests(ApiFactory factory)
{
    private readonly HttpClient _owner = factory.ClientFor(TestPersonas.RegularUser);
    private readonly HttpClient _public = factory.ClientFor(null);

    private async Task<(TestKey Key, WorkspaceSummary Workspace)> ArrangeWorkspaceAsync()
    {
        var key = TestKey.New();
        return (key, await _owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate()));
    }

    [Test]
    public async Task An_active_code_redirects_anonymously_with_a_302_to_the_destination()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());

        await _public.ShouldRedirectToAsync(link.Code, link.Destination);
    }

    [Test]
    public async Task Every_resolution_records_a_click_including_cache_hits()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        factory.DrainClicks();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"/{link.Code}");
        request.Headers.Referrer = new Uri("https://news.example.com/story");
        request.Headers.Add("CF-IPCountry", "AU");
        (await _public.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.Found);
        await _public.ShouldRedirectToAsync(link.Code, link.Destination); // second one is a cache hit

        var clicks = factory.DrainClicks();
        clicks.Count.ShouldBe(2);
        clicks.ShouldAllBe(c => c.LinkId == link.Id && c.Code == link.Code);
        clicks[0].Referrer.ShouldBe("https://news.example.com/story");
        clicks[0].Country.ShouldBe("AU");
    }

    [Test]
    public async Task An_unknown_code_shows_the_landing_page_and_records_no_click()
    {
        factory.DrainClicks();

        await _public.ShouldShowLandingPageAsync(TestKey.New().Code("missing"));

        factory.DrainClicks().ShouldBeEmpty();
    }

    [Test]
    public async Task A_code_created_after_a_miss_resolves_immediately()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var code = key.Code("later");
        await _public.ShouldShowLandingPageAsync(code); // negative-cached

        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = code });

        await _public.ShouldRedirectToAsync(code, link.Destination);
    }

    [Test]
    public async Task A_disabled_code_shows_the_landing_page()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        await _owner.SetActiveAsync(ws.Id, link.Id, isActive: false);

        await _public.ShouldShowLandingPageAsync(link.Code);
    }

    [Test]
    public async Task An_expired_code_shows_the_landing_page()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        // The API refuses a past expiry, so move the stored one into the past directly.
        await factory.WithDbAsync(db => db.Links.Where(l => l.Id == link.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.ExpiresOnUtc, DateTime.UtcNow.AddMinutes(-5))));

        await _public.ShouldShowLandingPageAsync(link.Code);
    }

    [Test]
    public async Task A_soft_deleted_code_shows_the_landing_page()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        await _public.ShouldRedirectToAsync(link.Code, link.Destination);

        await _owner.SoftDeleteLinkAsync(ws.Id, link.Id);

        await _public.ShouldShowLandingPageAsync(link.Code);
    }

    [Test]
    [Arguments("admin")]
    [Arguments("assets")]
    [Arguments("openapi")]
    [Arguments("swagger")]
    public async Task A_reserved_route_word_is_never_treated_as_a_code(string code)
    {
        await _public.ShouldShowLandingPageAsync(code);
    }

    [Test]
    [Arguments("not-base62")]
    [Arguments("a/b/c")]
    public async Task A_malformed_or_multi_segment_path_shows_the_landing_page(string path)
    {
        await _public.ShouldShowLandingPageAsync(path);
    }

    [Test]
    public async Task An_unknown_api_path_keeps_a_plain_404_not_the_landing_page()
    {
        var response = await _owner.GetAsync($"{ApiCalls.Api}/does-not-exist/at/all");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldNotBe("text/html");
    }

    [Test]
    public async Task Health_is_always_on_and_not_a_code()
    {
        var response = await _public.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldContain("ok");
    }
}
