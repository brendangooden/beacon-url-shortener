using System.Net;
using Shouldly;
using Beacon.Api.Domain;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class WorkspaceTests(ApiFactory factory)
{
    private const string WorkspacesPath = $"{ApiCalls.Api}/workspaces";

    [Test]
    public async Task Create_then_list_and_get_show_the_owner_as_manager()
    {
        var key = TestKey.New();
        var owner = TestPersonas.NewUser("owner");
        var client = factory.ClientFor(owner);
        var request = new WorkspaceRequestFaker(key).Generate();

        var created = await client.CreateWorkspaceAsync(request);

        created.Name.ShouldBe(request.Name);
        created.IsOwner.ShouldBeTrue();
        created.Access.ShouldBe(AccessLevel.Manager);

        var listed = await (await client.GetAsync(WorkspacesPath)).ReadAsync<List<WorkspaceSummary>>();
        listed.Where(w => w.Name.StartsWith(key.Value, StringComparison.Ordinal)).ShouldHaveSingleItem().Id.ShouldBe(created.Id);

        var detail = await (await client.GetAsync($"{WorkspacesPath}/{created.Id}")).ReadAsync<WorkspaceDetail>();
        detail.Name.ShouldBe(request.Name);
        detail.IsOwner.ShouldBeTrue();
        detail.Access.ShouldBe(AccessLevel.Manager);
        detail.FolderCount.ShouldBe(0);
        detail.LinkCount.ShouldBe(0);
    }

    [Test]
    public async Task Create_rejects_a_blank_name()
    {
        var response = await factory.ClientFor(TestPersonas.RegularUser).PostJsonAsync(WorkspacesPath, new CreateWorkspaceRequest("   "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task A_workspace_nobody_shared_with_you_is_404_and_not_listed()
    {
        var key = TestKey.New();
        var ws = await factory.ClientFor(TestPersonas.NewUser("owner")).CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var stranger = factory.ClientFor(TestPersonas.NewUser("stranger"));

        (await stranger.GetAsync($"{WorkspacesPath}/{ws.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var listed = await (await stranger.GetAsync(WorkspacesPath)).ReadAsync<List<WorkspaceSummary>>();
        listed.ShouldNotContain(w => w.Name.StartsWith(key.Value, StringComparison.Ordinal));
    }

    [Test]
    public async Task Rename_updates_the_name()
    {
        var key = TestKey.New();
        var client = factory.ClientFor(TestPersonas.RegularUser);
        var ws = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());

        var response = await client.PutJsonAsync($"{WorkspacesPath}/{ws.Id}", new RenameWorkspaceRequest(key.Name("Renamed")));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await client.GetAsync($"{WorkspacesPath}/{ws.Id}")).ReadAsync<WorkspaceDetail>()).Name.ShouldBe(key.Name("Renamed"));
    }

    [Test]
    public async Task Rename_and_delete_need_manager_access()
    {
        var key = TestKey.New();
        var ownerClient = factory.ClientFor(TestPersonas.NewUser("owner"));
        var editor = TestPersonas.NewUser("editor");
        var ws = await ownerClient.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        await ownerClient.ShareAsync(ResourceType.Workspace, ws.Id, editor, AccessLevel.Editor);
        var editorClient = factory.ClientFor(editor);

        (await editorClient.PutJsonAsync($"{WorkspacesPath}/{ws.Id}", new RenameWorkspaceRequest(key.Name("Nope"))))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await editorClient.DeleteAsync($"{WorkspacesPath}/{ws.Id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Delete_removes_the_workspace_and_its_links_stop_resolving()
    {
        var key = TestKey.New();
        var client = factory.ClientFor(TestPersonas.RegularUser);
        var ws = await client.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await client.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate() with { Code = key.Code("gone") });
        var publicClient = factory.ClientFor(null);
        await publicClient.ShouldRedirectToAsync(link.Code, link.Destination); // warm the cache first

        var response = await client.DeleteAsync($"{WorkspacesPath}/{ws.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"{WorkspacesPath}/{ws.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await publicClient.ShouldShowLandingPageAsync(link.Code);
    }
}
