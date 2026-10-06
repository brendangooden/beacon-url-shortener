using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Folders;

public sealed record CreateFolderRequest([property: Required, StringLength(200)] string Name);
public sealed record RenameFolderRequest([property: Required, StringLength(200)] string Name);
public sealed record FolderDto(Guid Id, Guid WorkspaceId, string Name, DateTime CreatedOnUtc, int LinkCount, AccessLevel Access, bool IsOwner);

public sealed class FolderService(AppDbContext db, ResourceAccess access, CurrentUser user)
{
    public async Task<IReadOnlyList<FolderDto>> ListAsync(Guid workspaceId, CancellationToken ct)
    {
        var wsAccess = await access.RequireWorkspaceAsync(workspaceId, AccessLevel.Viewer, ct);
        var rows = await db.Folders
            .Where(f => f.WorkspaceId == workspaceId)
            .OrderBy(f => f.Name)
            .Select(f => new { f.Id, f.WorkspaceId, f.Name, f.CreatedOnUtc, f.CreatedByOid, LinkCount = db.Links.Count(l => l.FolderId == f.Id) })
            .ToListAsync(ct);

        var me = user.Oid;
        var grantLevels = await GrantLevelsAsync(rows.Select(r => r.Id).ToList(), ct);

        return rows.Select(f =>
        {
            var isOwner = string.Equals(f.CreatedByOid, me, StringComparison.OrdinalIgnoreCase);
            var level = user.IsGlobalAdmin || isOwner ? AccessLevel.Manager : Max(wsAccess, grantLevels.GetValueOrDefault(f.Id, wsAccess));
            return new FolderDto(f.Id, f.WorkspaceId, f.Name, f.CreatedOnUtc, f.LinkCount, level, isOwner);
        }).ToList();
    }

    public async Task<FolderDto> CreateAsync(Guid workspaceId, CreateFolderRequest req, CancellationToken ct)
    {
        await access.RequireWorkspaceAsync(workspaceId, AccessLevel.Editor, ct);
        var folder = Folder.Create(workspaceId, req.Name, user.RequireOid());
        db.Folders.Add(folder);
        await db.SaveChangesAsync(ct);
        return new FolderDto(folder.Id, folder.WorkspaceId, folder.Name, folder.CreatedOnUtc, 0, AccessLevel.Manager, true);
    }

    public async Task RenameAsync(Guid workspaceId, Guid folderId, RenameFolderRequest req, CancellationToken ct)
    {
        await access.RequireFolderAsync(folderId, AccessLevel.Editor, ct);
        var folder = await db.Folders.AsTracking()
            .FirstOrDefaultAsync(f => f.Id == folderId && f.WorkspaceId == workspaceId, ct)
            ?? throw new NotFoundException("Folder not found.");
        folder.Rename(req.Name);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid workspaceId, Guid folderId, CancellationToken ct)
    {
        await access.RequireFolderAsync(folderId, AccessLevel.Manager, ct);
        var deleted = await db.Folders
            .Where(f => f.Id == folderId && f.WorkspaceId == workspaceId)
            .ExecuteDeleteAsync(ct);
        if (deleted == 0)
        {
            throw new NotFoundException("Folder not found.");
        }

        await db.ResourceGrants.Where(g => g.ResourceId == folderId).ExecuteDeleteAsync(ct);
        // Links in the folder fall back to the workspace root (FK SetNull); their codes are unchanged.
    }

    private async Task<Dictionary<Guid, AccessLevel>> GrantLevelsAsync(List<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var keys = new List<string>();
        if (user.Oid is { } oid)
        {
            keys.Add(oid);
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            keys.Add(user.Email.ToLowerInvariant());
        }

        var grants = await db.ResourceGrants
            .Where(g => g.ResourceType == ResourceType.Folder && keys.Contains(g.GranteeOid) && ids.Contains(g.ResourceId))
            .Select(g => new { g.ResourceId, g.Level })
            .ToListAsync(ct);
        return grants.GroupBy(g => g.ResourceId).ToDictionary(g => g.Key, g => g.Max(x => x.Level));
    }

    private static AccessLevel Max(AccessLevel a, AccessLevel b) => a >= b ? a : b;
}

public static class FolderEndpoints
{
    public static void MapFolderEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/workspaces/{workspaceId:guid}/folders").WithTags("Folders");

        group.MapGet("/", (Guid workspaceId, FolderService svc, CancellationToken ct) => svc.ListAsync(workspaceId, ct));
        group.MapPost("/", async (Guid workspaceId, CreateFolderRequest req, FolderService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateAsync(workspaceId, req, ct)));
        group.MapPut("/{folderId:guid}", async (Guid workspaceId, Guid folderId, RenameFolderRequest req, FolderService svc, CancellationToken ct) =>
        {
            await svc.RenameAsync(workspaceId, folderId, req, ct);
            return Results.NoContent();
        });
        group.MapDelete("/{folderId:guid}", async (Guid workspaceId, Guid folderId, FolderService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(workspaceId, folderId, ct);
            return Results.NoContent();
        });
    }
}

public static class FolderServiceExtensions
{
    public static IServiceCollection AddFolders(this IServiceCollection services) =>
        services.AddScoped<FolderService>();
}
