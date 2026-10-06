using System.Net;
using Shouldly;
using Beacon.Api.Domain;
using Beacon.Api.Features.Folders;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Sharing;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

/// <summary>Shares: grant / change / revoke, inheritance Workspace -> Folder -> Link, and Access-level enforcement.</summary>
[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class SharingTests(ApiFactory factory)
{
    private const string SharesPath = $"{ApiCalls.Api}/shares";

    private sealed record Tree(TestKey Key, WorkspaceSummary Workspace, FolderDto Folder, LinkDto FolderLink, LinkDto RootLink);

    /// <summary>A workspace with one folder; one link inside the folder and one at the workspace root.</summary>
    private static async Task<Tree> ArrangeTreeAsync(HttpClient owner)
    {
        var key = TestKey.New();
        var ws = await owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var folder = await owner.CreateFolderAsync(ws.Id, new FolderRequestFaker(key).Generate());
        var folderLink = await owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { FolderId = folder.Id });
        var rootLink = await owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        return new Tree(key, ws, folder, folderLink, rootLink);
    }

    private static UpdateLinkRequest Edit(LinkDto link, string destination) =>
        new(destination, link.FolderId, link.Title, link.Notes, link.Tags, null);

    [Test]
    public async Task Grant_change_and_revoke_a_workspace_share()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var grantee = TestPersonas.NewUser("grantee");
        var granteeClient = factory.ClientFor(grantee);
        var tree = await ArrangeTreeAsync(ownerClient);
        var wsPath = $"{ApiCalls.Api}/workspaces/{tree.Workspace.Id}";

        // Grant Viewer: can read, cannot create.
        var share = await ownerClient.ShareAsync(ResourceType.Workspace, tree.Workspace.Id, grantee, AccessLevel.Viewer);
        share.Level.ShouldBe(AccessLevel.Viewer);
        share.GranteeOid.ShouldBe(grantee.Oid);
        var detail = await (await granteeClient.GetAsync(wsPath)).ReadAsync<WorkspaceDetail>();
        detail.Access.ShouldBe(AccessLevel.Viewer);
        detail.IsOwner.ShouldBeFalse();
        (await granteeClient.PostJsonAsync(ApiCalls.LinksPath(tree.Workspace.Id), new LinkRequestFaker(tree.Key).Generate()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Change to Editor: can create now.
        (await ownerClient.PutJsonAsync($"{SharesPath}/{share.Id}", new ChangeShareRequest(AccessLevel.Editor)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await granteeClient.PostJsonAsync(ApiCalls.LinksPath(tree.Workspace.Id), new LinkRequestFaker(tree.Key).Generate()))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        // Revoke: the workspace disappears (404, not 403 — no existence leak).
        (await ownerClient.DeleteAsync($"{SharesPath}/{share.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await granteeClient.GetAsync(wsPath)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task List_shows_the_resources_shares_to_its_manager()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var a = TestPersonas.NewUser("a");
        var b = TestPersonas.NewUser("b");
        var tree = await ArrangeTreeAsync(ownerClient);
        await ownerClient.ShareAsync(ResourceType.Folder, tree.Folder.Id, a, AccessLevel.Viewer);
        await ownerClient.ShareAsync(ResourceType.Folder, tree.Folder.Id, b, AccessLevel.Manager);

        var shares = await (await ownerClient.GetAsync($"{SharesPath}?resourceType=Folder&resourceId={tree.Folder.Id}")).ReadAsync<List<ShareDto>>();

        shares.Select(s => (s.GranteeOid, s.Level)).OrderBy(s => s.GranteeOid, StringComparer.Ordinal)
            .ShouldBe(new[] { (a.Oid, AccessLevel.Viewer), (b.Oid, AccessLevel.Manager) }.OrderBy(s => s.Item1, StringComparer.Ordinal));
    }

    [Test]
    public async Task Sharing_the_same_person_twice_is_rejected()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var grantee = TestPersonas.NewUser("grantee");
        var tree = await ArrangeTreeAsync(ownerClient);
        await ownerClient.ShareAsync(ResourceType.Link, tree.RootLink.Id, grantee, AccessLevel.Viewer);

        var again = await ownerClient.PostJsonAsync(SharesPath,
            new CreateShareRequest(ResourceType.Link, tree.RootLink.Id, grantee.Email, grantee.Oid, grantee.Name, AccessLevel.Editor));

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Only_a_manager_can_manage_shares()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var editor = TestPersonas.NewUser("editor");
        var manager = TestPersonas.NewUser("manager");
        var tree = await ArrangeTreeAsync(ownerClient);
        await ownerClient.ShareAsync(ResourceType.Workspace, tree.Workspace.Id, editor, AccessLevel.Editor);
        await ownerClient.ShareAsync(ResourceType.Workspace, tree.Workspace.Id, manager, AccessLevel.Manager);

        (await factory.ClientFor(editor).PostJsonAsync(SharesPath,
                new CreateShareRequest(ResourceType.Workspace, tree.Workspace.Id, "someone@test.local", null, null, AccessLevel.Viewer)))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ClientFor(manager).PostJsonAsync(SharesPath,
                new CreateShareRequest(ResourceType.Workspace, tree.Workspace.Id, "someone@test.local", null, null, AccessLevel.Viewer)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_folder_share_covers_its_links_but_not_the_rest_of_the_workspace()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var editor = TestPersonas.NewUser("editor");
        var editorClient = factory.ClientFor(editor);
        var tree = await ArrangeTreeAsync(ownerClient);
        await ownerClient.ShareAsync(ResourceType.Folder, tree.Folder.Id, editor, AccessLevel.Editor);

        (await editorClient.PutJsonAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.FolderLink.Id), Edit(tree.FolderLink, "https://example.com/by-editor")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await editorClient.GetAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.RootLink.Id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await editorClient.GetAsync($"{ApiCalls.Api}/workspaces/{tree.Workspace.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task A_workspace_share_flows_down_to_folders_and_links()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var editor = TestPersonas.NewUser("editor");
        var editorClient = factory.ClientFor(editor);
        var tree = await ArrangeTreeAsync(ownerClient);
        await ownerClient.ShareAsync(ResourceType.Workspace, tree.Workspace.Id, editor, AccessLevel.Editor);

        (await editorClient.PutJsonAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.FolderLink.Id), Edit(tree.FolderLink, "https://example.com/f")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await editorClient.PutJsonAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.RootLink.Id), Edit(tree.RootLink, "https://example.com/r")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await editorClient.PutJsonAsync($"{ApiCalls.FoldersPath(tree.Workspace.Id)}/{tree.Folder.Id}", new RenameFolderRequest(tree.Key.Name("Edited"))))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var folders = await (await editorClient.GetAsync(ApiCalls.FoldersPath(tree.Workspace.Id))).ReadAsync<List<FolderDto>>();
        folders.ShouldContain(f => f.Id == tree.Folder.Id && f.Access == AccessLevel.Editor);
    }

    [Test]
    public async Task The_highest_level_wins_when_a_link_is_shared_directly_and_by_inheritance()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var person = TestPersonas.NewUser("person");
        var personClient = factory.ClientFor(person);
        var tree = await ArrangeTreeAsync(ownerClient);
        await ownerClient.ShareAsync(ResourceType.Workspace, tree.Workspace.Id, person, AccessLevel.Viewer);
        await ownerClient.ShareAsync(ResourceType.Link, tree.RootLink.Id, person, AccessLevel.Editor);

        (await personClient.PutJsonAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.RootLink.Id), Edit(tree.RootLink, "https://example.com/direct")))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await personClient.PutJsonAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.FolderLink.Id), Edit(tree.FolderLink, "https://example.com/inherited")))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Shared_with_me_lists_direct_shares_but_not_my_own_resources()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var person = TestPersonas.NewUser("person");
        var personClient = factory.ClientFor(person);
        var tree = await ArrangeTreeAsync(ownerClient);
        var mine = await personClient.CreateWorkspaceAsync(new WorkspaceRequestFaker(tree.Key).Generate());
        await ownerClient.ShareAsync(ResourceType.Folder, tree.Folder.Id, person, AccessLevel.Viewer);
        await ownerClient.ShareAsync(ResourceType.Link, tree.RootLink.Id, person, AccessLevel.Editor);

        var items = await (await personClient.GetAsync($"{SharesPath}/with-me")).ReadAsync<List<SharedWithMeItem>>();

        items.Select(i => (i.ResourceType, i.ResourceId, i.Level)).OrderBy(i => i.ResourceType).ShouldBe(
        [
            (ResourceType.Folder, tree.Folder.Id, AccessLevel.Viewer),
            (ResourceType.Link, tree.RootLink.Id, AccessLevel.Editor),
        ]);
        items.ShouldNotContain(i => i.ResourceId == mine.Id);
    }

    [Test]
    public async Task A_share_by_email_before_first_login_is_reconciled_when_the_person_signs_in()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var newcomer = TestPersonas.NewUser("newcomer");
        var tree = await ArrangeTreeAsync(ownerClient);

        var provisional = await (await ownerClient.PostJsonAsync(SharesPath,
                new CreateShareRequest(ResourceType.Workspace, tree.Workspace.Id, newcomer.Email.ToUpperInvariant(), null, null, AccessLevel.Viewer)))
            .ReadAsync<ShareDto>();
        provisional.GranteeOid.ShouldBe(newcomer.Email); // keyed on the lowercased email until login

        var newcomerClient = factory.ClientFor(newcomer);
        (await newcomerClient.GetAsync($"{ApiCalls.Api}/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var shares = await (await ownerClient.GetAsync($"{SharesPath}?resourceType=Workspace&resourceId={tree.Workspace.Id}")).ReadAsync<List<ShareDto>>();
        shares.ShouldHaveSingleItem().GranteeOid.ShouldBe(newcomer.Oid);
        (await newcomerClient.GetAsync($"{ApiCalls.Api}/workspaces/{tree.Workspace.Id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_global_admin_has_manager_access_everywhere_without_a_share()
    {
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var tree = await ArrangeTreeAsync(ownerClient);
        var admin = factory.ClientFor(TestPersonas.GlobalAdmin);

        var detail = await (await admin.GetAsync($"{ApiCalls.Api}/workspaces/{tree.Workspace.Id}")).ReadAsync<WorkspaceDetail>();

        detail.Access.ShouldBe(AccessLevel.Manager);
        (await admin.DeleteAsync(ApiCalls.LinkPath(tree.Workspace.Id, tree.RootLink.Id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }
}
