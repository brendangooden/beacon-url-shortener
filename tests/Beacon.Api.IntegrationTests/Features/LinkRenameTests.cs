using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Beacon.Api.Domain;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

/// <summary>
/// Rename + Retired codes (ADR-0004): the old ShortCode keeps forwarding in one hop, is never
/// reusable, follows the Link's active/trash state, and is freed only by a purge.
/// </summary>
[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class LinkRenameTests(ApiFactory factory)
{
    private readonly HttpClient _owner = factory.ClientFor(TestPersonas.RegularUser);
    private readonly HttpClient _admin = factory.ClientFor(TestPersonas.GlobalAdmin);
    private readonly HttpClient _public = factory.ClientFor(null);

    private async Task<(TestKey Key, WorkspaceSummary Workspace, LinkDto Link)> ArrangeLinkAsync(string code = "A")
    {
        var key = TestKey.New();
        var ws = await _owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code(code) });
        return (key, ws, link);
    }

    private static Task<HttpResponseMessage> PostRenameAsync(HttpClient client, Guid workspaceId, Guid linkId, string code) =>
        client.PostJsonAsync($"{ApiCalls.LinkPath(workspaceId, linkId)}/rename", new RenameLinkRequest(code));

    // ---- The happy path ----

    [Test]
    public async Task Rename_moves_the_old_code_to_a_retired_code_and_both_resolve()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _public.ShouldRedirectToAsync(link.Code, link.Destination); // old code cached as current

        var renamed = await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));

        renamed.Id.ShouldBe(link.Id);
        renamed.Code.ShouldBe(key.Code("B"));
        renamed.ShortUrl.ShouldBe($"{ApiFactory.ShortBaseUrl}/{key.Code("B")}");
        await _public.ShouldRedirectToAsync(key.Code("B"), link.Destination);
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);

        var retired = await factory.WithDbAsync(db => db.RetiredCodes.Where(rc => rc.LinkId == link.Id).Select(rc => rc.Code).ToListAsync());
        retired.ShouldBe([key.Code("A")]);
    }

    [Test]
    public async Task Renaming_twice_sends_every_retired_code_straight_to_the_destination()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("C"));

        // One hop: the Location is the Destination itself, never a short URL of a newer code.
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);
        await _public.ShouldRedirectToAsync(key.Code("B"), link.Destination);
        await _public.ShouldRedirectToAsync(key.Code("C"), link.Destination);
    }

    [Test]
    public async Task A_retired_code_follows_a_later_destination_change()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination); // retired code cached

        await _owner.PutJsonAsync(ApiCalls.LinkPath(ws.Id, link.Id),
            new UpdateLinkRequest("https://example.com/moved", null, link.Title, link.Notes, link.Tags, null));

        await _public.ShouldRedirectToAsync(key.Code("A"), "https://example.com/moved");
        await _public.ShouldRedirectToAsync(key.Code("B"), "https://example.com/moved");
    }

    [Test]
    public async Task A_click_on_a_retired_code_is_recorded_against_the_link_with_the_code_used()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        factory.DrainClicks();

        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);

        var click = factory.DrainClicks().ShouldHaveSingleItem();
        click.LinkId.ShouldBe(link.Id);
        click.Code.ShouldBe(key.Code("A"));
    }

    // ---- Retired codes are never reusable ----

    [Test]
    public async Task Create_with_a_retired_code_as_the_vanity_code_is_rejected()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));

        var response = await _owner.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Code = key.Code("A") });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("already taken");
    }

    [Test]
    public async Task Another_link_cannot_rename_to_a_retired_code()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        var other = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));

        var response = await PostRenameAsync(_owner, ws.Id, other.Id, key.Code("A"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("already taken");
    }

    [Test]
    public async Task A_link_cannot_rename_back_to_its_own_retired_code()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));

        var response = await PostRenameAsync(_owner, ws.Id, link.Id, key.Code("A"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await (await _owner.GetAsync(ApiCalls.LinkPath(ws.Id, link.Id))).ReadAsync<LinkDto>()).Code.ShouldBe(key.Code("B"));
    }

    [Test]
    public async Task Renaming_to_the_current_code_is_rejected()
    {
        var (_, ws, link) = await ArrangeLinkAsync();

        var response = await PostRenameAsync(_owner, ws.Id, link.Id, link.Code);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("already this link's current code");
    }

    [Test]
    public async Task Renaming_to_another_links_current_code_is_rejected()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        var other = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("taken") });

        var response = await PostRenameAsync(_owner, ws.Id, link.Id, other.Code);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    [Arguments("admin")]
    [Arguments("health")]
    [Arguments("no-dash")]
    [Arguments("ab")]
    public async Task Renaming_to_a_reserved_or_malformed_code_is_rejected(string code)
    {
        var (key, ws, link) = await ArrangeLinkAsync();

        var response = await PostRenameAsync(_owner, ws.Id, link.Id, code);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);
    }

    // ---- Access level ----

    [Test]
    public async Task Rename_needs_editor_access()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        var viewer = TestPersonas.NewUser("viewer");
        var editor = TestPersonas.NewUser("editor");
        await _owner.ShareAsync(ResourceType.Link, link.Id, viewer, AccessLevel.Viewer);
        await _owner.ShareAsync(ResourceType.Link, link.Id, editor, AccessLevel.Editor);

        (await PostRenameAsync(factory.ClientFor(viewer), ws.Id, link.Id, key.Code("V"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await PostRenameAsync(factory.ClientFor(editor), ws.Id, link.Id, key.Code("E"))).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PostRenameAsync(factory.ClientFor(TestPersonas.NewUser("stranger")), ws.Id, link.Id, key.Code("S")))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Interaction with disable / trash / purge ----

    [Test]
    public async Task Disabling_stops_current_and_retired_codes_and_enabling_restores_both()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);
        await _public.ShouldRedirectToAsync(key.Code("B"), link.Destination);

        await _owner.SetActiveAsync(ws.Id, link.Id, isActive: false);
        await _public.ShouldShowLandingPageAsync(key.Code("A"));
        await _public.ShouldShowLandingPageAsync(key.Code("B"));

        await _owner.SetActiveAsync(ws.Id, link.Id, isActive: true);
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);
        await _public.ShouldRedirectToAsync(key.Code("B"), link.Destination);
    }

    [Test]
    public async Task Soft_delete_stops_current_and_retired_codes_and_restore_brings_both_back()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);

        await _owner.SoftDeleteLinkAsync(ws.Id, link.Id);
        await _public.ShouldShowLandingPageAsync(key.Code("A"));
        await _public.ShouldShowLandingPageAsync(key.Code("B"));

        (await _admin.PostAsync($"{ApiCalls.Api}/admin/links/{link.Id}/restore", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await _public.ShouldRedirectToAsync(key.Code("A"), link.Destination);
        await _public.ShouldRedirectToAsync(key.Code("B"), link.Destination);
    }

    [Test]
    public async Task Soft_delete_keeps_retired_codes_reserved()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        await _owner.SoftDeleteLinkAsync(ws.Id, link.Id);

        (await _owner.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Code = key.Code("A") }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _owner.PostJsonAsync(ApiCalls.LinksPath(ws.Id), new LinkRequestFaker(key).Generate() with { Code = key.Code("B") }))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Purge_frees_the_current_and_every_retired_code()
    {
        var (key, ws, link) = await ArrangeLinkAsync();
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("B"));
        await _owner.RenameLinkAsync(ws.Id, link.Id, key.Code("C"));
        await _owner.SoftDeleteLinkAsync(ws.Id, link.Id);

        (await _admin.DeleteAsync($"{ApiCalls.Api}/admin/links/{link.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await factory.WithDbAsync(db => db.RetiredCodes.CountAsync(rc => rc.LinkId == link.Id))).ShouldBe(0);
        foreach (var code in new[] { key.Code("A"), key.Code("B"), key.Code("C") })
        {
            await _public.ShouldShowLandingPageAsync(code);
            var reused = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = code });
            reused.Id.ShouldNotBe(link.Id);
            await _public.ShouldRedirectToAsync(code, reused.Destination);
        }
    }
}
