using Microsoft.EntityFrameworkCore;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Analytics;

public sealed record CountByKey(string Key, long Count);
public sealed record DayCount(DateOnly Day, long Count);
public sealed record TopLink(Guid LinkId, string Code, string Destination, long Clicks);

public sealed record LinkAnalytics(
    Guid LinkId, string Code, long TotalClicks, long UniqueVisitors, int WindowDays,
    IReadOnlyList<DayCount> ClicksByDay, IReadOnlyList<CountByKey> TopReferrers,
    IReadOnlyList<CountByKey> ByBrowser, IReadOnlyList<CountByKey> ByOs,
    IReadOnlyList<CountByKey> ByDevice, IReadOnlyList<CountByKey> ByCountry);

public sealed record WorkspaceAnalytics(
    Guid WorkspaceId, long TotalClicks, int TotalLinks, int ActiveLinks, int WindowDays,
    IReadOnlyList<TopLink> TopLinks, IReadOnlyList<DayCount> ClicksByDay);

public sealed class AnalyticsService(AppDbContext db, ResourceAccess access)
{
    private const int TopN = 10;
    private const int MinDays = 1;
    private const int MaxDays = 365;
    private const int DefaultDays = 30;

    public async Task<LinkAnalytics> ForLinkAsync(Guid workspaceId, Guid linkId, int? days, CancellationToken ct)
    {
        await access.RequireLinkAsync(linkId, AccessLevel.Viewer, ct);

        var link = await db.Links
            .Where(l => l.Id == linkId && l.WorkspaceId == workspaceId)
            .Select(l => new { l.Id, l.Code })
            .FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Link not found.");

        var window = ClampDays(days);
        var since = DateTime.UtcNow.AddDays(-window);
        var clicks = db.Clicks.Where(c => c.LinkId == linkId && c.TimestampUtc >= since);

        var total = await clicks.LongCountAsync(ct);
        var unique = await clicks.Where(c => c.IpHash != null).Select(c => c.IpHash).Distinct().LongCountAsync(ct);

        return new LinkAnalytics(
            link.Id, link.Code, total, unique, window,
            await ByDayAsync(clicks, ct),
            await TopAsync(clicks, c => c.Referrer, ct),
            await TopAsync(clicks, c => c.Browser, ct),
            await TopAsync(clicks, c => c.Os, ct),
            await TopAsync(clicks, c => c.DeviceType, ct),
            await TopAsync(clicks, c => c.Country, ct));
    }

    public async Task<WorkspaceAnalytics> ForWorkspaceAsync(Guid workspaceId, int? days, CancellationToken ct)
    {
        await access.RequireWorkspaceAsync(workspaceId, AccessLevel.Viewer, ct);

        var window = ClampDays(days);
        var since = DateTime.UtcNow.AddDays(-window);

        var totalLinks = await db.Links.CountAsync(l => l.WorkspaceId == workspaceId, ct);
        var activeLinks = await db.Links.CountAsync(l => l.WorkspaceId == workspaceId && l.IsActive, ct);

        var clicks = db.Clicks
            .Where(c => c.TimestampUtc >= since &&
                        db.Links.Any(l => l.Id == c.LinkId && l.WorkspaceId == workspaceId));

        var total = await clicks.LongCountAsync(ct);

        var topLinks = await clicks
            .GroupBy(c => c.LinkId)
            .Select(g => new { LinkId = g.Key, Clicks = g.LongCount() })
            .OrderByDescending(x => x.Clicks)
            .Take(TopN)
            .Join(db.Links, x => x.LinkId, l => l.Id, (x, l) => new TopLink(l.Id, l.Code, l.Destination, x.Clicks))
            .ToListAsync(ct);

        return new WorkspaceAnalytics(workspaceId, total, totalLinks, activeLinks, window, topLinks, await ByDayAsync(clicks, ct));
    }

    private static async Task<IReadOnlyList<DayCount>> ByDayAsync(IQueryable<Click> clicks, CancellationToken ct)
    {
        var rows = await clicks
            .GroupBy(c => c.TimestampUtc.Date)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .OrderBy(x => x.Key)
            .ToListAsync(ct);
        return rows.Select(x => new DayCount(DateOnly.FromDateTime(x.Key), x.Count)).ToList();
    }

    private static async Task<IReadOnlyList<CountByKey>> TopAsync(
        IQueryable<Click> clicks, System.Linq.Expressions.Expression<Func<Click, string?>> selector, CancellationToken ct)
    {
        // EF only translates GroupBy immediately followed by an aggregate Select with the key used
        // as-is (no transformation, no Where between). So project { Key, Count } here and do the
        // null-drop + DTO mapping client-side. Take TopN+1 so removing the single null group still
        // leaves TopN real rows.
        var rows = await clicks
            .GroupBy(selector)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .OrderByDescending(x => x.Count)
            .Take(TopN + 1)
            .ToListAsync(ct);

        return rows.Where(x => x.Key != null).Select(x => new CountByKey(x.Key!, x.Count)).Take(TopN).ToList();
    }

    private static int ClampDays(int? days) => Math.Clamp(days ?? DefaultDays, MinDays, MaxDays);
}

public static class AnalyticsEndpoints
{
    public static void MapAnalyticsEndpoints(this IEndpointRouteBuilder api)
    {
        api.MapGet("/workspaces/{workspaceId:guid}/links/{linkId:guid}/analytics",
            (Guid workspaceId, Guid linkId, int? days, AnalyticsService svc, CancellationToken ct) =>
                svc.ForLinkAsync(workspaceId, linkId, days, ct))
            .WithTags("Analytics");

        api.MapGet("/workspaces/{workspaceId:guid}/analytics",
            (Guid workspaceId, int? days, AnalyticsService svc, CancellationToken ct) =>
                svc.ForWorkspaceAsync(workspaceId, days, ct))
            .WithTags("Analytics");
    }
}

public static class AnalyticsServiceExtensions
{
    public static IServiceCollection AddAnalytics(this IServiceCollection services) =>
        services.AddScoped<AnalyticsService>();
}
