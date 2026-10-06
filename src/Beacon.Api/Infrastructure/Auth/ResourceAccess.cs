using Microsoft.EntityFrameworkCore;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Infrastructure.Auth;

/// <summary>
/// Resolves the current user's effective <see cref="AccessLevel"/> on a resource. Effective access
/// is the highest of: Global-Admin (Manager), ownership of the resource or an ancestor (Manager),
/// a direct grant, or a grant inherited from an ancestor (Folder -&gt; Workspace). No access = null.
/// Grants added by email before the grantee's first login are keyed on the lowercased email, so we
/// match on both the real oid and that provisional key.
/// </summary>
public sealed class ResourceAccess(AppDbContext db, CurrentUser user)
{
    private string? Oid => user.Oid;

    private List<string> MyGranteeKeys()
    {
        var keys = new List<string>();
        if (!string.IsNullOrEmpty(Oid))
        {
            keys.Add(Oid);
        }

        var email = user.Email;
        if (!string.IsNullOrWhiteSpace(email))
        {
            keys.Add(email.ToLowerInvariant());
        }

        return keys;
    }

    public async Task<AccessLevel?> OnWorkspaceAsync(Guid workspaceId, CancellationToken ct)
    {
        var owner = await db.Workspaces.Where(w => w.Id == workspaceId).Select(w => w.CreatedByOid).FirstOrDefaultAsync(ct);
        if (owner is null)
        {
            return null;
        }

        return await ResolveAsync([owner], [workspaceId], ct);
    }

    public async Task<AccessLevel?> OnFolderAsync(Guid folderId, CancellationToken ct)
    {
        var f = await db.Folders.Where(x => x.Id == folderId)
            .Select(x => new
            {
                x.Id,
                x.WorkspaceId,
                x.CreatedByOid,
                WsOwner = db.Workspaces.Where(w => w.Id == x.WorkspaceId).Select(w => w.CreatedByOid).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (f is null)
        {
            return null;
        }

        return await ResolveAsync([f.CreatedByOid, f.WsOwner], [f.Id, f.WorkspaceId], ct);
    }

    public async Task<AccessLevel?> OnLinkAsync(Guid linkId, CancellationToken ct)
    {
        var l = await db.Links.Where(x => x.Id == linkId)
            .Select(x => new
            {
                x.Id,
                x.WorkspaceId,
                x.FolderId,
                x.CreatedByOid,
                WsOwner = db.Workspaces.Where(w => w.Id == x.WorkspaceId).Select(w => w.CreatedByOid).FirstOrDefault(),
                FolderOwner = x.FolderId == null ? null : db.Folders.Where(fo => fo.Id == x.FolderId).Select(fo => fo.CreatedByOid).FirstOrDefault(),
            })
            .FirstOrDefaultAsync(ct);
        if (l is null)
        {
            return null;
        }

        var owners = new List<string?> { l.CreatedByOid, l.WsOwner, l.FolderOwner };
        var nodes = new List<Guid> { l.Id, l.WorkspaceId };
        if (l.FolderId is { } fid)
        {
            nodes.Add(fid);
        }

        return await ResolveAsync(owners, nodes, ct);
    }

    /// <summary>
    /// Workspaces this user personally owns or has been shared — NOT every workspace a Global-Admin
    /// could reach. This is the "my workspaces" set that drives the sidebar; a Global-Admin browses
    /// everything else through the admin surface instead of drowning the rail in every user's list.
    /// </summary>
    public async Task<List<Guid>> OwnedOrSharedWorkspaceIdsAsync(CancellationToken ct)
    {
        var me = Oid;
        if (me is null)
        {
            return [];
        }

        var keys = MyGranteeKeys();
        var owned = db.Workspaces.Where(w => w.CreatedByOid == me).Select(w => w.Id);
        var granted = db.ResourceGrants
            .Where(g => g.ResourceType == ResourceType.Workspace && keys.Contains(g.GranteeOid))
            .Select(g => g.ResourceId);
        return await owned.Union(granted).ToListAsync(ct);
    }

    public async Task<AccessLevel> RequireWorkspaceAsync(Guid id, AccessLevel min, CancellationToken ct) =>
        Guard(await OnWorkspaceAsync(id, ct), min, "Workspace");

    public async Task<AccessLevel> RequireFolderAsync(Guid id, AccessLevel min, CancellationToken ct) =>
        Guard(await OnFolderAsync(id, ct), min, "Folder");

    public async Task<AccessLevel> RequireLinkAsync(Guid id, AccessLevel min, CancellationToken ct) =>
        Guard(await OnLinkAsync(id, ct), min, "Link");

    /// <summary>Effective access on any resource by type (used by the sharing feature).</summary>
    public Task<AccessLevel?> OnResourceAsync(ResourceType type, Guid id, CancellationToken ct) => type switch
    {
        ResourceType.Workspace => OnWorkspaceAsync(id, ct),
        ResourceType.Folder => OnFolderAsync(id, ct),
        ResourceType.Link => OnLinkAsync(id, ct),
        _ => Task.FromResult<AccessLevel?>(null),
    };

    private async Task<AccessLevel?> ResolveAsync(IEnumerable<string?> ownerOids, IReadOnlyList<Guid> nodeIds, CancellationToken ct)
    {
        if (user.IsGlobalAdmin)
        {
            return AccessLevel.Manager;
        }

        var me = Oid;
        if (me is null)
        {
            return null;
        }

        if (ownerOids.Any(o => !string.IsNullOrEmpty(o) && string.Equals(o, me, StringComparison.OrdinalIgnoreCase)))
        {
            return AccessLevel.Manager;
        }

        var keys = MyGranteeKeys();
        var ids = nodeIds.ToList();
        var levels = await db.ResourceGrants
            .Where(g => keys.Contains(g.GranteeOid) && ids.Contains(g.ResourceId))
            .Select(g => g.Level)
            .ToListAsync(ct);

        return levels.Count == 0 ? null : levels.Max();
    }

    private static AccessLevel Guard(AccessLevel? level, AccessLevel min, string what)
    {
        if (level is null)
        {
            throw new NotFoundException($"{what} not found.");
        }

        if (level < min)
        {
            throw new ForbiddenException($"This action requires {min} access on the {what.ToLowerInvariant()}.");
        }

        return level.Value;
    }
}
