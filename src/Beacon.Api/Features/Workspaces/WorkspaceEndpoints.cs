using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Caching;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Workspaces;

public sealed record CreateWorkspaceRequest([property: Required, StringLength(200)] string Name);
public sealed record RenameWorkspaceRequest([property: Required, StringLength(200)] string Name);
public sealed record WorkspaceSummary(
    Guid Id, string Name, bool IsPersonal, AccessLevel Access, bool IsOwner, int LinkCount,
    string? OwnerName, string? OwnerEmail);
public sealed record WorkspaceDetail(
    Guid Id, string Name, bool IsPersonal, AccessLevel Access, bool IsOwner, DateTime CreatedOnUtc,
    int FolderCount, int LinkCount, string? OwnerName, string? OwnerEmail);

public sealed class WorkspaceService(AppDbContext db, ResourceAccess access, CurrentUser user, LinkCache linkCache)
{
    public async Task<IReadOnlyList<WorkspaceSummary>> ListAsync(CancellationToken ct)
    {
        // The sidebar shows the user's own + shared workspaces — even for a Global-Admin. A GA reaches
        // every other workspace through the admin "Browse all workspaces" surface (AdminEndpoints).
        var ids = await access.OwnedOrSharedWorkspaceIdsAsync(ct);
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Workspaces
            .Where(w => ids.Contains(w.Id))
            .OrderByDescending(w => w.IsPersonal).ThenBy(w => w.Name)
            .Select(w => new { w.Id, w.Name, w.IsPersonal, w.CreatedByOid, LinkCount = db.Links.Count(l => l.WorkspaceId == w.Id) })
            .ToListAsync(ct);

        var me = user.Oid;
        var grantLevels = await GrantLevelsAsync(ResourceType.Workspace, ids, ct);

        var ownerOids = rows.Select(w => w.CreatedByOid).Distinct().ToList();
        var owners = await db.Users.Where(u => ownerOids.Contains(u.Oid))
            .Select(u => new { u.Oid, u.Name, u.Email })
            .ToDictionaryAsync(u => u.Oid, ct);

        return rows.Select(w =>
        {
            var isOwner = string.Equals(w.CreatedByOid, me, StringComparison.OrdinalIgnoreCase);
            var granted = grantLevels.GetValueOrDefault(w.Id, AccessLevel.Viewer);
            var level = user.IsGlobalAdmin || isOwner ? AccessLevel.Manager : granted;
            var owner = owners.GetValueOrDefault(w.CreatedByOid);
            return new WorkspaceSummary(w.Id, w.Name, w.IsPersonal, level, isOwner, w.LinkCount, owner?.Name, owner?.Email);
        }).ToList();
    }

    public async Task<WorkspaceSummary> CreateAsync(CreateWorkspaceRequest req, CancellationToken ct)
    {
        var workspace = Workspace.Create(req.Name, user.RequireOid());
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
        return new WorkspaceSummary(workspace.Id, workspace.Name, workspace.IsPersonal, AccessLevel.Manager, true, 0, user.Name, user.Email);
    }

    public async Task<WorkspaceDetail> GetAsync(Guid id, CancellationToken ct)
    {
        var level = await access.RequireWorkspaceAsync(id, AccessLevel.Viewer, ct);
        var w = await db.Workspaces.Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Name, x.IsPersonal, x.CreatedByOid, x.CreatedOnUtc })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Workspace not found.");
        var isOwner = string.Equals(w.CreatedByOid, user.Oid, StringComparison.OrdinalIgnoreCase);
        var folderCount = await db.Folders.CountAsync(f => f.WorkspaceId == id, ct);
        var linkCount = await db.Links.CountAsync(l => l.WorkspaceId == id, ct);
        var owner = await db.Users.Where(u => u.Oid == w.CreatedByOid).Select(u => new { u.Name, u.Email }).FirstOrDefaultAsync(ct);
        return new WorkspaceDetail(w.Id, w.Name, w.IsPersonal, level, isOwner, w.CreatedOnUtc, folderCount, linkCount, owner?.Name, owner?.Email);
    }

    public async Task RenameAsync(Guid id, RenameWorkspaceRequest req, CancellationToken ct)
    {
        await access.RequireWorkspaceAsync(id, AccessLevel.Manager, ct);
        var workspace = await db.Workspaces.AsTracking().FirstOrDefaultAsync(w => w.Id == id, ct)
            ?? throw new NotFoundException("Workspace not found.");
        workspace.Rename(req.Name);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        await access.RequireWorkspaceAsync(id, AccessLevel.Manager, ct);

        var codes = await db.Links.Where(l => l.WorkspaceId == id).Select(l => l.Code).ToListAsync(ct);
        var folderIds = await db.Folders.Where(f => f.WorkspaceId == id).Select(f => f.Id).ToListAsync(ct);
        var linkIds = await db.Links.Where(l => l.WorkspaceId == id).Select(l => l.Id).ToListAsync(ct);

        await db.Workspaces.Where(w => w.Id == id).ExecuteDeleteAsync(ct);

        // Clean up grants for the workspace and its (now-deleted) descendants.
        var resourceIds = new List<Guid> { id };
        resourceIds.AddRange(folderIds);
        resourceIds.AddRange(linkIds);
        await db.ResourceGrants.Where(g => resourceIds.Contains(g.ResourceId)).ExecuteDeleteAsync(ct);

        foreach (var code in codes)
        {
            await linkCache.InvalidateAsync(code, ct);
        }
    }

    private async Task<Dictionary<Guid, AccessLevel>> GrantLevelsAsync(ResourceType type, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var keys = MyKeys();
        var grants = await db.ResourceGrants
            .Where(g => g.ResourceType == type && keys.Contains(g.GranteeOid) && ids.Contains(g.ResourceId))
            .Select(g => new { g.ResourceId, g.Level })
            .ToListAsync(ct);
        return grants.GroupBy(g => g.ResourceId).ToDictionary(g => g.Key, g => g.Max(x => x.Level));
    }

    private List<string> MyKeys()
    {
        var keys = new List<string>();
        if (user.Oid is { } oid)
        {
            keys.Add(oid);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            keys.Add(user.Email.ToLowerInvariant());
        }

        return keys;
    }
}

public static class WorkspaceEndpoints
{
    public static void MapWorkspaceEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/workspaces").WithTags("Workspaces");

        group.MapGet("/", (WorkspaceService svc, CancellationToken ct) => svc.ListAsync(ct));
        group.MapPost("/", async (CreateWorkspaceRequest req, WorkspaceService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateAsync(req, ct)));
        group.MapGet("/{id:guid}", (Guid id, WorkspaceService svc, CancellationToken ct) => svc.GetAsync(id, ct));
        group.MapPut("/{id:guid}", async (Guid id, RenameWorkspaceRequest req, WorkspaceService svc, CancellationToken ct) =>
        {
            await svc.RenameAsync(id, req, ct);
            return Results.NoContent();
        });
        group.MapDelete("/{id:guid}", async (Guid id, WorkspaceService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(id, ct);
            return Results.NoContent();
        });
    }
}

public static class WorkspaceServiceExtensions
{
    public static IServiceCollection AddWorkspaces(this IServiceCollection services) =>
        services.AddScoped<WorkspaceService>();
}
