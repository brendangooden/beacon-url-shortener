using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Caching;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Admin;

// Cross-workspace admin views. Global-Admin only — every row is visible regardless of ownership.
public sealed record AdminLinkRow(
    Guid Id, Guid WorkspaceId, string WorkspaceName, string? OwnerName, string Code, string ShortUrl,
    string Destination, string? Title, bool IsActive, long ClickCount, DateTime CreatedOnUtc);

public sealed record AdminWorkspaceRow(
    Guid Id, string Name, bool IsPersonal, string? OwnerName, string? OwnerEmail, int FolderCount,
    int LinkCount, DateTime CreatedOnUtc);

public sealed record DeletedLinkRow(
    Guid Id, string WorkspaceName, string? OwnerName, string Code, string ShortUrl, string Destination,
    string? Title, long ClickCount, DateTime DeletedOnUtc, string? DeletedByName);

public sealed class AdminService(AppDbContext db, CurrentUser user, LinkCache cache, IOptions<ShortUrlOptions> shortUrl)
{
    private readonly string _baseUrl = shortUrl.Value.BaseUrl.TrimEnd('/');

    private void RequireGlobalAdmin()
    {
        if (!user.IsGlobalAdmin)
        {
            throw new ForbiddenException("This view is for Global Admins only.");
        }
    }

    public async Task<IReadOnlyList<AdminLinkRow>> AllLinksAsync(string? search, CancellationToken ct)
    {
        RequireGlobalAdmin();

        var query =
            from l in db.Links
            join w in db.Workspaces on l.WorkspaceId equals w.Id
            select new { Link = l, WorkspaceName = w.Name, OwnerOid = w.CreatedByOid };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.Link.Code, $"%{term}%") ||
                EF.Functions.ILike(x.Link.Destination, $"%{term}%") ||
                (x.Link.Title != null && EF.Functions.ILike(x.Link.Title, $"%{term}%")) ||
                EF.Functions.ILike(x.WorkspaceName, $"%{term}%"));
        }

        var rows = await query.OrderByDescending(x => x.Link.CreatedOnUtc).Take(1000).ToListAsync(ct);

        var ownerOids = rows.Select(x => x.OwnerOid).Distinct().ToList();
        var ownerNames = await db.Users.Where(u => ownerOids.Contains(u.Oid))
            .Select(u => new { u.Oid, u.Name })
            .ToDictionaryAsync(u => u.Oid, u => u.Name, ct);

        return rows.Select(x => new AdminLinkRow(
            x.Link.Id, x.Link.WorkspaceId, x.WorkspaceName, ownerNames.GetValueOrDefault(x.OwnerOid),
            x.Link.Code, $"{_baseUrl}/{x.Link.Code}", x.Link.Destination, x.Link.Title,
            x.Link.IsActive, x.Link.ClickCount, x.Link.CreatedOnUtc)).ToList();
    }

    public async Task<IReadOnlyList<AdminWorkspaceRow>> AllWorkspacesAsync(string? search, CancellationToken ct)
    {
        RequireGlobalAdmin();

        var query = db.Workspaces.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(w => EF.Functions.ILike(w.Name, $"%{term}%"));
        }

        var rows = await query
            .OrderByDescending(w => w.IsPersonal).ThenBy(w => w.Name)
            .Select(w => new
            {
                w.Id,
                w.Name,
                w.IsPersonal,
                w.CreatedByOid,
                w.CreatedOnUtc,
                FolderCount = db.Folders.Count(f => f.WorkspaceId == w.Id),
                LinkCount = db.Links.Count(l => l.WorkspaceId == w.Id),
            })
            .Take(1000)
            .ToListAsync(ct);

        var ownerOids = rows.Select(w => w.CreatedByOid).Distinct().ToList();
        var owners = await db.Users.Where(u => ownerOids.Contains(u.Oid))
            .Select(u => new { u.Oid, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Oid, ct);

        return rows.Select(w =>
        {
            var owner = owners.GetValueOrDefault(w.CreatedByOid);
            return new AdminWorkspaceRow(w.Id, w.Name, w.IsPersonal, owner?.Name, owner?.Email, w.FolderCount, w.LinkCount, w.CreatedOnUtc);
        }).ToList();
    }

    // ---- Trash (soft-deleted links) ----
    public async Task<IReadOnlyList<DeletedLinkRow>> DeletedLinksAsync(CancellationToken ct)
    {
        RequireGlobalAdmin();

        var rows = await (
            from l in db.Links.IgnoreQueryFilters()
            join w in db.Workspaces on l.WorkspaceId equals w.Id
            where l.DeletedOnUtc != null
            orderby l.DeletedOnUtc descending
            select new { Link = l, WorkspaceName = w.Name, OwnerOid = w.CreatedByOid })
            .Take(500)
            .ToListAsync(ct);

        var oids = rows.SelectMany(x => new[] { x.OwnerOid, x.Link.DeletedByOid })
            .Where(o => o is not null).Select(o => o!).Distinct().ToList();
        var names = await db.Users.Where(u => oids.Contains(u.Oid)).ToDictionaryAsync(u => u.Oid, u => u.Name, ct);

        return rows.Select(x => new DeletedLinkRow(
            x.Link.Id, x.WorkspaceName, names.GetValueOrDefault(x.OwnerOid), x.Link.Code, $"{_baseUrl}/{x.Link.Code}",
            x.Link.Destination, x.Link.Title, x.Link.ClickCount, x.Link.DeletedOnUtc!.Value,
            x.Link.DeletedByOid is null ? null : names.GetValueOrDefault(x.Link.DeletedByOid))).ToList();
    }

    public async Task RestoreLinkAsync(Guid id, CancellationToken ct)
    {
        RequireGlobalAdmin();
        var link = await db.Links.IgnoreQueryFilters().AsTracking().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Link not found.");
        if (!link.IsDeleted)
        {
            return;
        }

        link.Restore();
        db.LinkEvents.Add(LinkEvent.Record(id, LinkEventType.Restored, user.RequireOid(), user.Name));
        await db.SaveChangesAsync(ct);
        // Clear the negative-cache miss on every code (current + retired) so they all resolve again.
        await cache.InvalidateAllAsync(link.Id, link.Code, ct);
    }

    public async Task PurgeLinkAsync(Guid id, CancellationToken ct)
    {
        RequireGlobalAdmin();
        var link = await db.Links.IgnoreQueryFilters().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw new NotFoundException("Link not found.");
        var retiredCodes = await db.RetiredCodes.Where(rc => rc.LinkId == id).Select(rc => rc.Code).ToListAsync(ct);

        // Permanent: cascades clicks, events, and retired codes (see ADR-0004); drop its shares too.
        await db.Links.IgnoreQueryFilters().Where(l => l.Id == id).ExecuteDeleteAsync(ct);
        await db.ResourceGrants.Where(g => g.ResourceId == id).ExecuteDeleteAsync(ct);
        await cache.InvalidateAsync(link.Code, ct);
        foreach (var code in retiredCodes)
        {
            await cache.InvalidateAsync(code, ct);
        }
    }
}

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/admin").WithTags("Admin");
        group.MapGet("/links", (string? search, AdminService svc, CancellationToken ct) => svc.AllLinksAsync(search, ct));
        group.MapGet("/workspaces", (string? search, AdminService svc, CancellationToken ct) => svc.AllWorkspacesAsync(search, ct));
        group.MapGet("/links/deleted", (AdminService svc, CancellationToken ct) => svc.DeletedLinksAsync(ct));
        group.MapPost("/links/{id:guid}/restore", async (Guid id, AdminService svc, CancellationToken ct) =>
        {
            await svc.RestoreLinkAsync(id, ct);
            return Results.NoContent();
        });
        group.MapDelete("/links/{id:guid}", async (Guid id, AdminService svc, CancellationToken ct) =>
        {
            await svc.PurgeLinkAsync(id, ct);
            return Results.NoContent();
        });
    }
}

public static class AdminServiceExtensions
{
    public static IServiceCollection AddAdmin(this IServiceCollection services) =>
        services.AddScoped<AdminService>();
}
