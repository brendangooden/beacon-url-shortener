using System.Net;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Beacon.Api.Domain;
using Beacon.Api.Features.Admin;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

/// <summary>Global-Admin-only views plus the trash: list, restore, purge.</summary>
[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class AdminTests(ApiFactory factory)
{
    private const string AdminPath = $"{ApiCalls.Api}/admin";

    private readonly HttpClient _owner = factory.ClientFor(TestPersonas.RegularUser);
    private readonly HttpClient _admin = factory.ClientFor(TestPersonas.GlobalAdmin);

    private async Task<(TestKey Key, WorkspaceSummary Workspace, LinkDto Link)> ArrangeTrashedLinkAsync()
    {
        var key = TestKey.New();
        var ws = await _owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("trash") });
        await _owner.SoftDeleteLinkAsync(ws.Id, link.Id);
        return (key, ws, link);
    }

    // ---- Guards ----

    public static IEnumerable<Func<(string Method, string Path)>> AdminRoutes()
    {
        var id = Guid.NewGuid();
        yield return () => ("GET", $"{AdminPath}/links");
        yield return () => ("GET", $"{AdminPath}/workspaces");
        yield return () => ("GET", $"{AdminPath}/links/deleted");
        yield return () => ("POST", $"{AdminPath}/links/{id}/restore");
        yield return () => ("DELETE", $"{AdminPath}/links/{id}");
    }

    [Test]
    [MethodDataSource(nameof(AdminRoutes))]
    public async Task A_regular_user_gets_403_on_every_admin_route((string Method, string Path) route)
    {
        using var request = new HttpRequestMessage(new HttpMethod(route.Method), route.Path);

        var response = await _owner.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Cross-workspace views ----

    [Test]
    public async Task All_links_shows_every_users_links_with_workspace_and_owner()
    {
        var key = TestKey.New();
        var someone = TestPersonas.NewUser("someone");
        var client = factory.ClientFor(someone);
        await client.GetAsync($"{ApiCalls.Api}/me"); // register the display name
        var ws = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("all") });

        var rows = await (await _admin.GetAsync($"{AdminPath}/links?search={key.Value}")).ReadAsync<List<AdminLinkRow>>();

        var row = rows.ShouldHaveSingleItem();
        row.Id.ShouldBe(link.Id);
        row.WorkspaceName.ShouldBe(ws.Name);
        row.OwnerName.ShouldBe(someone.Name);
    }

    [Test]
    public async Task All_workspaces_counts_only_live_links()
    {
        var key = TestKey.New();
        var ws = await _owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        await _owner.CreateFolderAsync(ws.Id, new FolderRequestFaker(key).Generate());
        await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        var trashed = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        await _owner.SoftDeleteLinkAsync(ws.Id, trashed.Id);

        var rows = await (await _admin.GetAsync($"{AdminPath}/workspaces?search={key.Value}")).ReadAsync<List<AdminWorkspaceRow>>();

        var row = rows.ShouldHaveSingleItem();
        row.Id.ShouldBe(ws.Id);
        row.FolderCount.ShouldBe(1);
        row.LinkCount.ShouldBe(1);
    }

    // ---- Trash ----

    [Test]
    public async Task Trash_lists_soft_deleted_links_with_who_deleted_them()
    {
        await _owner.GetAsync($"{ApiCalls.Api}/me"); // register the display name
        var (_, ws, link) = await ArrangeTrashedLinkAsync();

        var rows = await (await _admin.GetAsync($"{AdminPath}/links/deleted")).ReadAsync<List<DeletedLinkRow>>();

        var row = rows.Where(r => r.Id == link.Id).ShouldHaveSingleItem();
        row.Code.ShouldBe(link.Code);
        row.WorkspaceName.ShouldBe(ws.Name);
        row.DeletedByName.ShouldBe(TestPersonas.RegularUser.Name);

        var live = await (await _admin.GetAsync($"{AdminPath}/links?search={link.Code}")).ReadAsync<List<AdminLinkRow>>();
        live.ShouldBeEmpty();
    }

    [Test]
    public async Task Restore_brings_the_link_back_everywhere_and_records_an_event()
    {
        var (key, ws, link) = await ArrangeTrashedLinkAsync();
        var publicClient = factory.ClientFor(null);
        await publicClient.ShouldShowLandingPageAsync(link.Code); // negative-cached while trashed

        (await _admin.PostAsync($"{AdminPath}/links/{link.Id}/restore", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await publicClient.ShouldRedirectToAsync(link.Code, link.Destination);
        var listed = await (await _owner.GetAsync($"{ApiCalls.LinksPath(ws.Id)}?search={key.Value}")).ReadAsync<List<LinkDto>>();
        listed.ShouldHaveSingleItem().Id.ShouldBe(link.Id);
        var deleted = await (await _admin.GetAsync($"{AdminPath}/links/deleted")).ReadAsync<List<DeletedLinkRow>>();
        deleted.ShouldNotContain(r => r.Id == link.Id);
        var events = await (await _owner.GetAsync($"{ApiCalls.LinkPath(ws.Id, link.Id)}/events")).ReadAsync<List<LinkEventDto>>();
        events.Select(e => e.Type).Take(2).ShouldBe([nameof(LinkEventType.Restored), nameof(LinkEventType.Deleted)]);
    }

    [Test]
    public async Task Restore_of_an_unknown_link_is_404()
    {
        (await _admin.PostAsync($"{AdminPath}/links/{Guid.NewGuid()}/restore", null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Purge_deletes_the_link_with_its_clicks_events_retired_codes_and_shares()
    {
        var (key, ws, link) = await ArrangeTrashedLinkAsync();
        await factory.WithDbAsync(async db =>
        {
            db.Clicks.Add(new Click(link.Id, link.Code, DateTime.UtcNow));
            db.RetiredCodes.Add(RetiredCode.Create(link.Id, key.Code("old")));
            db.ResourceGrants.Add(ResourceGrant.Create(ResourceType.Link, link.Id, "someone", "someone@test.local", "Someone", AccessLevel.Viewer, "granter"));
            await db.SaveChangesAsync();
        });

        (await _admin.DeleteAsync($"{AdminPath}/links/{link.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var remaining = await factory.WithDbAsync(async db => new
        {
            Links = await db.Links.IgnoreQueryFilters().CountAsync(l => l.Id == link.Id),
            Clicks = await db.Clicks.CountAsync(c => c.LinkId == link.Id),
            Events = await db.LinkEvents.CountAsync(e => e.LinkId == link.Id),
            Retired = await db.RetiredCodes.CountAsync(rc => rc.LinkId == link.Id),
            Shares = await db.ResourceGrants.CountAsync(g => g.ResourceId == link.Id),
        });
        remaining.ShouldBe(new { Links = 0, Clicks = 0, Events = 0, Retired = 0, Shares = 0 });

        // The code is free again.
        var reused = await _owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = link.Code });
        reused.Id.ShouldNotBe(link.Id);
    }
}
