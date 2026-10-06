using System.Net;
using Shouldly;
using Beacon.Api.Domain;
using Beacon.Api.Features.Folders;
using Beacon.Api.Features.Links;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class FolderTests(ApiFactory factory)
{
    [Test]
    public async Task Create_and_list_show_the_folder_with_its_link_count()
    {
        var key = TestKey.New();
        var client = factory.ClientFor(TestPersonas.RegularUser);
        var ws = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var folder = await client.CreateFolderAsync(ws.Id, new FolderRequestFaker(key).Generate());
        await client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { FolderId = folder.Id });
        await client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { FolderId = folder.Id });
        await client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate()); // workspace root

        var folders = await (await client.GetAsync(ApiCalls.FoldersPath(ws.Id))).ReadAsync<List<FolderDto>>();

        var listed = folders.Where(f => f.Name.StartsWith(key.Value, StringComparison.Ordinal)).ShouldHaveSingleItem();
        listed.Id.ShouldBe(folder.Id);
        listed.LinkCount.ShouldBe(2);
        listed.IsOwner.ShouldBeTrue();
        listed.Access.ShouldBe(AccessLevel.Manager);

        var detail = await (await client.GetAsync($"{ApiCalls.Api}/workspaces/{ws.Id}")).ReadAsync<WorkspaceDetail>();
        detail.FolderCount.ShouldBe(1);
        detail.LinkCount.ShouldBe(3);
    }

    [Test]
    public async Task Rename_updates_the_name()
    {
        var key = TestKey.New();
        var client = factory.ClientFor(TestPersonas.RegularUser);
        var ws = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var folder = await client.CreateFolderAsync(ws.Id, new FolderRequestFaker(key).Generate());

        var response = await client.PutJsonAsync($"{ApiCalls.FoldersPath(ws.Id)}/{folder.Id}", new RenameFolderRequest(key.Name("Renamed")));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var folders = await (await client.GetAsync(ApiCalls.FoldersPath(ws.Id))).ReadAsync<List<FolderDto>>();
        folders.ShouldContain(f => f.Id == folder.Id && f.Name == key.Name("Renamed"));
    }

    [Test]
    public async Task Delete_moves_its_links_to_the_workspace_root_and_they_keep_resolving()
    {
        var key = TestKey.New();
        var client = factory.ClientFor(TestPersonas.RegularUser);
        var ws = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var folder = await client.CreateFolderAsync(ws.Id, new FolderRequestFaker(key).Generate());
        var link = await client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { FolderId = folder.Id });

        var response = await client.DeleteAsync($"{ApiCalls.FoldersPath(ws.Id)}/{folder.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var after = await (await client.GetAsync(ApiCalls.LinkPath(ws.Id, link.Id))).ReadAsync<LinkDto>();
        after.FolderId.ShouldBeNull();
        after.Code.ShouldBe(link.Code);
        await factory.ClientFor(null).ShouldRedirectToAsync(link.Code, link.Destination);
    }

    [Test]
    public async Task Viewer_cannot_create_a_folder_but_editor_can()
    {
        var key = TestKey.New();
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var viewer = TestPersonas.NewUser("viewer");
        var editor = TestPersonas.NewUser("editor");
        var ws = await ownerClient.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        await ownerClient.ShareAsync(ResourceType.Workspace, ws.Id, viewer, AccessLevel.Viewer);
        await ownerClient.ShareAsync(ResourceType.Workspace, ws.Id, editor, AccessLevel.Editor);

        (await factory.ClientFor(viewer).PostJsonAsync(ApiCalls.FoldersPath(ws.Id), new FolderRequestFaker(key).Generate()))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ClientFor(editor).PostJsonAsync(ApiCalls.FoldersPath(ws.Id), new FolderRequestFaker(key).Generate()))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task A_link_cannot_be_put_in_a_folder_from_another_workspace()
    {
        var key = TestKey.New();
        var client = factory.ClientFor(TestPersonas.RegularUser);
        var wsA = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var wsB = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var folderInB = await client.CreateFolderAsync(wsB.Id, new FolderRequestFaker(key).Generate());

        var response = await client.PostJsonAsync(ApiCalls.LinksPath(wsA.Id), new LinkRequestFaker(key).Generate() with { FolderId = folderInB.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
