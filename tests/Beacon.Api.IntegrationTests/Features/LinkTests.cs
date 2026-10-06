using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class LinkTests(ApiFactory factory)
{
    private readonly HttpClient _client = factory.ClientFor(TestPersonas.RegularUser);

    private async Task<(TestKey Key, WorkspaceSummary Workspace)> ArrangeWorkspaceAsync()
    {
        var key = TestKey.New();
        return (key, await _client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate()));
    }

    // ---- Create ----

    [Test]
    public async Task Create_without_a_code_generates_a_base62_short_code()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var request = new LinkRequestFaker(key).Generate();

        var link = await _client.CreateLinkAsync(ws.Id, request);

        link.Code.Length.ShouldBe(ShortCodes.DefaultLength);
        ShortCodes.IsWellFormed(link.Code).ShouldBeTrue();
        link.ShortUrl.ShouldBe($"{ApiFactory.ShortBaseUrl}/{link.Code}");
        link.Destination.ShouldBe(request.Destination);
        link.Title.ShouldBe(request.Title);
        link.Tags.ShouldBe(request.Tags!.Distinct(StringComparer.Ordinal));
        link.IsActive.ShouldBeTrue();
        link.WorkspaceId.ShouldBe(ws.Id);
    }

    [Test]
    public async Task Create_with_a_vanity_code_uses_it_and_the_code_resolves()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();

        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("Spring") });

        link.Code.ShouldBe(key.Code("Spring"));
        await factory.ClientFor(null).ShouldRedirectToAsync(link.Code, link.Destination);
    }

    [Test]
    public async Task Create_rejects_a_vanity_code_that_is_already_taken()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("dup") });

        var response = await _client.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Code = key.Code("dup") });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("already taken");
    }

    [Test]
    [Arguments("admin")]
    [Arguments("api")]
    [Arguments("ab")]
    [Arguments("has-dash")]
    public async Task Create_rejects_a_reserved_or_malformed_vanity_code(string code)
    {
        var (key, ws) = await ArrangeWorkspaceAsync();

        var response = await _client.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Code = code });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("not a url")]
    [Arguments("ftp://example.com/file")]
    public async Task Create_rejects_a_destination_that_is_not_http(string destination)
    {
        var (key, ws) = await ArrangeWorkspaceAsync();

        var response = await _client.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Destination = destination });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Create_rejects_an_expiry_in_the_past()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();

        var response = await _client.PostJsonAsync(ApiCalls.LinksPath(ws.Id),
            new LinkRequestFaker(key).Generate() with { ExpiresOnUtc = DateTime.UtcNow.AddHours(-1) });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- List / get ----

    [Test]
    public async Task List_search_finds_only_the_matching_links()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var other = TestKey.New();
        await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("one") });
        await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("two") });
        await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(other).Generate() with { Code = other.Code("x") });

        var found = await (await _client.GetAsync($"{ApiCalls.LinksPath(ws.Id)}?search={key.Value}")).ReadAsync<List<LinkDto>>();

        found.Select(l => l.Code).Order(StringComparer.Ordinal).ShouldBe([key.Code("one"), key.Code("two")]);
    }

    [Test]
    public async Task Get_a_link_through_another_workspace_is_404()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var otherWs = await _client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());

        (await _client.GetAsync(ApiCalls.LinkPath(otherWs.Id, link.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Update ----

    [Test]
    public async Task Update_changes_the_destination_and_the_redirect_follows_it()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        var publicClient = factory.ClientFor(null);
        await publicClient.ShouldRedirectToAsync(link.Code, link.Destination); // cached now

        var response = await _client.PutJsonAsync(ApiCalls.LinkPath(ws.Id, link.Id),
            new UpdateLinkRequest("https://example.com/new-home", null, key.Name("New title"), null, ["fresh"], null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsync<LinkDto>();
        updated.Destination.ShouldBe("https://example.com/new-home");
        updated.Title.ShouldBe(key.Name("New title"));
        updated.Tags.ShouldBe(["fresh"]);
        updated.Code.ShouldBe(link.Code);
        await publicClient.ShouldRedirectToAsync(link.Code, "https://example.com/new-home");
    }

    // ---- Active / inactive ----

    [Test]
    public async Task Disabling_stops_the_redirect_and_enabling_brings_it_back()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        var publicClient = factory.ClientFor(null);
        await publicClient.ShouldRedirectToAsync(link.Code, link.Destination);

        await _client.SetActiveAsync(ws.Id, link.Id, isActive: false);
        await publicClient.ShouldShowLandingPageAsync(link.Code);
        (await (await _client.GetAsync(ApiCalls.LinkPath(ws.Id, link.Id))).ReadAsync<LinkDto>()).IsActive.ShouldBeFalse();

        await _client.SetActiveAsync(ws.Id, link.Id, isActive: true);
        await publicClient.ShouldRedirectToAsync(link.Code, link.Destination);
    }

    // ---- Soft delete ----

    [Test]
    public async Task Soft_delete_hides_the_link_from_list_get_counts_and_redirect()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var kept = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        var trashed = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        var publicClient = factory.ClientFor(null);
        await publicClient.ShouldRedirectToAsync(trashed.Code, trashed.Destination);

        await _client.SoftDeleteLinkAsync(ws.Id, trashed.Id);

        var listed = await (await _client.GetAsync($"{ApiCalls.LinksPath(ws.Id)}?search={key.Value}")).ReadAsync<List<LinkDto>>();
        listed.Select(l => l.Id).ShouldBe([kept.Id]);
        (await _client.GetAsync(ApiCalls.LinkPath(ws.Id, trashed.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await _client.GetAsync($"{ApiCalls.Api}/workspaces/{ws.Id}")).ReadAsync<WorkspaceDetail>()).LinkCount.ShouldBe(1);
        await publicClient.ShouldShowLandingPageAsync(trashed.Code);
    }

    [Test]
    public async Task A_trashed_links_code_stays_taken_until_purged()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var trashed = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("held") });
        await _client.SoftDeleteLinkAsync(ws.Id, trashed.Id);

        var response = await _client.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Code = key.Code("held") });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Soft_delete_needs_manager_access()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var editor = TestPersonas.NewUser("editor");
        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        await _client.ShareAsync(ResourceType.Workspace, ws.Id, editor, AccessLevel.Editor);

        (await factory.ClientFor(editor).DeleteAsync(ApiCalls.LinkPath(ws.Id, link.Id))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Audit trail ----

    [Test]
    public async Task Events_record_each_change_with_actor_and_values_newest_first()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("audit") });
        await _client.PutJsonAsync(ApiCalls.LinkPath(ws.Id, link.Id),
            new UpdateLinkRequest("https://example.com/changed", null, link.Title, link.Notes, link.Tags, null));
        await _client.SetActiveAsync(ws.Id, link.Id, isActive: false);
        await _client.RenameLinkAsync(ws.Id, link.Id, key.Code("audit2"));

        var events = await (await _client.GetAsync($"{ApiCalls.LinkPath(ws.Id, link.Id)}/events")).ReadAsync<List<LinkEventDto>>();

        events.Select(e => e.Type).ShouldBe(
        [
            nameof(LinkEventType.Renamed), nameof(LinkEventType.Disabled),
            nameof(LinkEventType.DestinationChanged), nameof(LinkEventType.Created),
        ]);
        events.ShouldAllBe(e => e.ActorName == TestPersonas.RegularUser.Name);
        var renamed = events[0];
        renamed.OldValue.ShouldBe(key.Code("audit"));
        renamed.NewValue.ShouldBe(key.Code("audit2"));
        var changed = events[2];
        changed.OldValue.ShouldBe(link.Destination);
        changed.NewValue.ShouldBe("https://example.com/changed");
    }

    [Test]
    public async Task An_update_that_changes_nothing_records_no_event()
    {
        var (key, ws) = await ArrangeWorkspaceAsync();
        var link = await _client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());

        await _client.PutJsonAsync(ApiCalls.LinkPath(ws.Id, link.Id),
            new UpdateLinkRequest(link.Destination, null, link.Title, link.Notes, link.Tags, null));

        var count = await factory.WithDbAsync(db => db.LinkEvents.CountAsync(e => e.LinkId == link.Id));
        count.ShouldBe(1); // only Created
    }
}
