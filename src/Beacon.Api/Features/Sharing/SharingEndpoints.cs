using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Sharing;

public sealed record ShareDto(Guid Id, ResourceType ResourceType, Guid ResourceId, string GranteeOid, string GranteeEmail, string GranteeName, AccessLevel Level, DateTime CreatedOnUtc);
public sealed record CreateShareRequest(ResourceType ResourceType, Guid ResourceId, [property: Required, EmailAddress] string Email, string? Oid, string? Name, AccessLevel Level);
public sealed record ChangeShareRequest(AccessLevel Level);
public sealed record SharedWithMeItem(ResourceType ResourceType, Guid ResourceId, Guid? WorkspaceId, string Name, string? Detail, AccessLevel Level);

public sealed class SharingService(AppDbContext db, ResourceAccess access, CurrentUser user, IOptions<SharingOptions> sharingOptions)
{
    private readonly string? _allowedDomain = sharingOptions.Value.NormalisedDomain;

    public async Task<IReadOnlyList<ShareDto>> ListAsync(ResourceType type, Guid resourceId, CancellationToken ct)
    {
        await RequireManagerAsync(type, resourceId, ct);
        return await db.ResourceGrants
            .Where(g => g.ResourceType == type && g.ResourceId == resourceId)
            .OrderBy(g => g.GranteeEmail)
            .Select(g => new ShareDto(g.Id, g.ResourceType, g.ResourceId, g.GranteeOid, g.GranteeEmail, g.GranteeName, g.Level, g.CreatedOnUtc))
            .ToListAsync(ct);
    }

    public async Task<ShareDto> CreateAsync(CreateShareRequest req, CancellationToken ct)
    {
        await RequireManagerAsync(req.ResourceType, req.ResourceId, ct);

        // Enforce the allowed-domain policy (defence-in-depth; the SPA also collects only the local part).
        if (_allowedDomain is not null)
        {
            var emailDomain = req.Email.Split('@').LastOrDefault();
            if (!string.Equals(emailDomain, _allowedDomain, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException($"You can only share with @{_allowedDomain} addresses.");
            }
        }

        // Key on the real oid when known, else provisionally on the lowercased email (login reconciles it).
        var oid = string.IsNullOrWhiteSpace(req.Oid) ? req.Email.ToLowerInvariant() : req.Oid;
        var email = req.Email.ToLowerInvariant();

        var already = await db.ResourceGrants.AnyAsync(
            g => g.ResourceType == req.ResourceType && g.ResourceId == req.ResourceId &&
                 (g.GranteeOid == oid || g.GranteeEmail == email), ct);
        if (already)
        {
            throw new DomainException("That user already has access to this resource.");
        }

        var grant = ResourceGrant.Create(req.ResourceType, req.ResourceId, oid, email, req.Name ?? req.Email, req.Level, user.RequireOid());
        db.ResourceGrants.Add(grant);
        await db.SaveChangesAsync(ct);
        return new ShareDto(grant.Id, grant.ResourceType, grant.ResourceId, grant.GranteeOid, grant.GranteeEmail, grant.GranteeName, grant.Level, grant.CreatedOnUtc);
    }

    public async Task ChangeAsync(Guid grantId, ChangeShareRequest req, CancellationToken ct)
    {
        var grant = await db.ResourceGrants.AsTracking().FirstOrDefaultAsync(g => g.Id == grantId, ct)
            ?? throw new NotFoundException("Share not found.");
        await RequireManagerAsync(grant.ResourceType, grant.ResourceId, ct);
        grant.ChangeLevel(req.Level);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid grantId, CancellationToken ct)
    {
        var grant = await db.ResourceGrants.FirstOrDefaultAsync(g => g.Id == grantId, ct)
            ?? throw new NotFoundException("Share not found.");
        await RequireManagerAsync(grant.ResourceType, grant.ResourceId, ct);
        await db.ResourceGrants.Where(g => g.Id == grantId).ExecuteDeleteAsync(ct);
    }

    /// <summary>Resources shared directly with the current user (that they do not own).</summary>
    public async Task<IReadOnlyList<SharedWithMeItem>> SharedWithMeAsync(CancellationToken ct)
    {
        var me = user.Oid;
        if (me is null)
        {
            return [];
        }

        var keys = new List<string> { me };
        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            keys.Add(user.Email.ToLowerInvariant());
        }

        var grants = await db.ResourceGrants
            .Where(g => keys.Contains(g.GranteeOid))
            .Select(g => new { g.ResourceType, g.ResourceId, g.Level })
            .ToListAsync(ct);

        var items = new List<SharedWithMeItem>();

        var wsIds = grants.Where(g => g.ResourceType == ResourceType.Workspace).Select(g => g.ResourceId).ToList();
        if (wsIds.Count > 0)
        {
            var wss = await db.Workspaces.Where(w => wsIds.Contains(w.Id) && w.CreatedByOid != me)
                .Select(w => new { w.Id, w.Name }).ToListAsync(ct);
            items.AddRange(wss.Select(w => new SharedWithMeItem(ResourceType.Workspace, w.Id, null, w.Name, null,
                grants.First(g => g.ResourceType == ResourceType.Workspace && g.ResourceId == w.Id).Level)));
        }

        var folderIds = grants.Where(g => g.ResourceType == ResourceType.Folder).Select(g => g.ResourceId).ToList();
        if (folderIds.Count > 0)
        {
            var fs = await db.Folders.Where(f => folderIds.Contains(f.Id) && f.CreatedByOid != me)
                .Select(f => new { f.Id, f.Name, f.WorkspaceId }).ToListAsync(ct);
            items.AddRange(fs.Select(f => new SharedWithMeItem(ResourceType.Folder, f.Id, f.WorkspaceId, f.Name, null,
                grants.First(g => g.ResourceType == ResourceType.Folder && g.ResourceId == f.Id).Level)));
        }

        var linkIds = grants.Where(g => g.ResourceType == ResourceType.Link).Select(g => g.ResourceId).ToList();
        if (linkIds.Count > 0)
        {
            var ls = await db.Links.Where(l => linkIds.Contains(l.Id) && l.CreatedByOid != me)
                .Select(l => new { l.Id, l.Code, l.Destination, l.WorkspaceId }).ToListAsync(ct);
            items.AddRange(ls.Select(l => new SharedWithMeItem(ResourceType.Link, l.Id, l.WorkspaceId, l.Code, l.Destination,
                grants.First(g => g.ResourceType == ResourceType.Link && g.ResourceId == l.Id).Level)));
        }

        return items;
    }

    private async Task RequireManagerAsync(ResourceType type, Guid id, CancellationToken ct)
    {
        var level = await access.OnResourceAsync(type, id, ct);
        if (level is null)
        {
            throw new NotFoundException("Resource not found.");
        }

        if (level < AccessLevel.Manager)
        {
            throw new ForbiddenException("Only a Manager (or owner) can manage sharing.");
        }
    }
}

public static class SharingEndpoints
{
    public static void MapSharingEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/shares").WithTags("Sharing");

        group.MapGet("/", (ResourceType resourceType, Guid resourceId, SharingService svc, CancellationToken ct) =>
            svc.ListAsync(resourceType, resourceId, ct));
        group.MapGet("/with-me", (SharingService svc, CancellationToken ct) => svc.SharedWithMeAsync(ct));
        group.MapPost("/", async (CreateShareRequest req, SharingService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateAsync(req, ct)));
        group.MapPut("/{grantId:guid}", async (Guid grantId, ChangeShareRequest req, SharingService svc, CancellationToken ct) =>
        {
            await svc.ChangeAsync(grantId, req, ct);
            return Results.NoContent();
        });
        group.MapDelete("/{grantId:guid}", async (Guid grantId, SharingService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(grantId, ct);
            return Results.NoContent();
        });
    }
}

public static class SharingServiceExtensions
{
    public static IServiceCollection AddSharing(this IServiceCollection services) =>
        services.AddScoped<SharingService>();
}
