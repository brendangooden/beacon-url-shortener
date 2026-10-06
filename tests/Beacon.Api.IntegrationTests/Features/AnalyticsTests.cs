using System.Net;
using Shouldly;
using Beacon.Api.Domain;
using Beacon.Api.Features.Analytics;
using Beacon.TestInfrastructure;
using Beacon.TestInfrastructure.Fakers;

namespace Beacon.Api.IntegrationTests.Features;

/// <summary>
/// Click analytics read side. Clicks are seeded straight into the DB (the background writer is off
/// in tests), so the counts are exact.
/// </summary>
[ClassDataSource<ApiFactory>(Shared = SharedType.PerClass)]
public sealed class AnalyticsTests(ApiFactory factory)
{
    [Test]
    public async Task Link_analytics_counts_clicks_in_the_window_for_a_viewer()
    {
        var key = TestKey.New();
        var owner = factory.ClientFor(TestPersonas.RegularUser);
        var viewer = TestPersonas.NewUser("viewer");
        var ws = await owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());
        await owner.ShareAsync(ResourceType.Link, link.Id, viewer, AccessLevel.Viewer);
        await factory.WithDbAsync(async db =>
        {
            var now = DateTime.UtcNow;
            db.Clicks.AddRange(
                new Click(link.Id, link.Code, now.AddHours(-1)) { IpHash = "a", Referrer = "https://news.example.com", Browser = "Firefox" },
                new Click(link.Id, link.Code, now.AddHours(-2)) { IpHash = "a", Referrer = "https://news.example.com", Browser = "Chrome" },
                new Click(link.Id, link.Code, now.AddHours(-3)) { IpHash = "b", Browser = "Firefox" },
                new Click(link.Id, link.Code, now.AddDays(-60)) { IpHash = "c" }); // outside the 30-day default window
            await db.SaveChangesAsync();
        });

        var response = await factory.ClientFor(viewer).GetAsync($"{ApiCalls.LinkPath(ws.Id, link.Id)}/analytics");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var analytics = await response.ReadAsync<LinkAnalytics>();
        analytics.TotalClicks.ShouldBe(3);
        analytics.UniqueVisitors.ShouldBe(2);
        analytics.ByBrowser.ShouldContain(b => b.Key == "Firefox" && b.Count == 2);
        analytics.TopReferrers.ShouldContain(r => r.Key == "https://news.example.com" && r.Count == 2);
    }

    [Test]
    public async Task Analytics_for_someone_elses_link_is_404()
    {
        var key = TestKey.New();
        var owner = factory.ClientFor(TestPersonas.RegularUser);
        var ws = await owner.CreateWorkspaceAsync(new WorkspaceRequestFaker(key).Generate());
        var link = await owner.CreateLinkAsync(ws.Id, new LinkRequestFaker(key).Generate());

        var response = await factory.ClientFor(TestPersonas.NewUser("stranger")).GetAsync($"{ApiCalls.LinkPath(ws.Id, link.Id)}/analytics");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
