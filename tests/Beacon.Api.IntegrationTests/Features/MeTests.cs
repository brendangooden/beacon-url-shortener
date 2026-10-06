using System.Net;
using Shouldly;
using Beacon.Api.Features.Me;
using Beacon.Api.Features.Workspaces;
using Beacon.TestInfrastructure;

namespace Beacon.Api.IntegrationTests.Features;

[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class MeTests(ApiFactory factory)
{
    private const string MePath = $"{ApiCalls.Api}/me";

    [Test]
    public async Task Anonymous_request_to_the_api_is_401()
    {
        var response = await factory.ClientFor(null).GetAsync(MePath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Test]
    public async Task First_login_creates_a_personal_workspace_once()
    {
        var user = TestPersonas.NewUser("first");
        var client = factory.ClientFor(user);

        var me = await (await client.GetAsync(MePath)).ReadAsync<MeResponse>();
        await client.GetAsync(MePath); // a second call must not create a second one

        me.Oid.ShouldBe(user.Oid);
        me.Email.ShouldBe(user.Email);
        me.IsGlobalAdmin.ShouldBeFalse();

        var workspaces = await (await client.GetAsync($"{ApiCalls.Api}/workspaces")).ReadAsync<List<WorkspaceSummary>>();
        var personal = workspaces.Where(w => w.IsPersonal).ShouldHaveSingleItem();
        personal.IsOwner.ShouldBeTrue();
        personal.Access.ShouldBe(Domain.AccessLevel.Manager);
    }

    [Test]
    public async Task Global_admin_is_recognised_from_the_configured_allowlist()
    {
        var me = await (await factory.ClientFor(TestPersonas.GlobalAdmin).GetAsync(MePath)).ReadAsync<MeResponse>();

        me.IsGlobalAdmin.ShouldBeTrue();
    }

    [Test]
    public async Task Regular_user_is_not_a_global_admin()
    {
        var me = await (await factory.ClientFor(TestPersonas.RegularUser).GetAsync(MePath)).ReadAsync<MeResponse>();

        me.IsGlobalAdmin.ShouldBeFalse();
    }
}
