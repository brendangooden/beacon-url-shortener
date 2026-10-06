using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Beacon.Api.Common;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Auth;
using Beacon.Api.Infrastructure.Caching;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Features.Links;

// ---- DTOs ----
public sealed record CreateLinkRequest(
    [property: Required, Url, StringLength(2048)] string Destination,
    string? Code,
    Guid? FolderId,
    [property: StringLength(300)] string? Title,
    [property: StringLength(2000)] string? Notes,
    IReadOnlyList<string>? Tags,
    DateTime? ExpiresOnUtc);

public sealed record UpdateLinkRequest(
    [property: Required, Url, StringLength(2048)] string Destination,
    Guid? FolderId,
    [property: StringLength(300)] string? Title,
    [property: StringLength(2000)] string? Notes,
    IReadOnlyList<string>? Tags,
    DateTime? ExpiresOnUtc);

public sealed record SetActiveRequest(bool IsActive);

public sealed record RenameLinkRequest([property: Required, StringLength(64)] string Code);

public sealed record LinkDto(
    Guid Id, Guid WorkspaceId, Guid? FolderId, string Code, string ShortUrl, string Destination,
    string? Title, string? Notes, IReadOnlyList<string> Tags, bool IsActive, DateTime? ExpiresOnUtc,
    long ClickCount, DateTime CreatedOnUtc, DateTime? UpdatedOnUtc);

public sealed record LinkEventDto(
    string Type, string ActorName, string? OldValue, string? NewValue, DateTime CreatedOnUtc);

// ---- Service ----
public sealed class LinkService(AppDbContext db, ResourceAccess access, CurrentUser user, LinkCache cache, IOptions<ShortUrlOptions> shortUrl)
{
    private const int MaxCodeAttempts = 6;
    private readonly string _baseUrl = shortUrl.Value.BaseUrl.TrimEnd('/');

    public async Task<IReadOnlyList<LinkDto>> ListAsync(Guid workspaceId, Guid? folderId, string? search, CancellationToken ct)
    {
        await access.RequireWorkspaceAsync(workspaceId, AccessLevel.Viewer, ct);

        var query = db.Links.Where(l => l.WorkspaceId == workspaceId);
        if (folderId is { } fid)
        {
            query = query.Where(l => l.FolderId == fid);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                EF.Functions.ILike(l.Code, $"%{term}%") ||
                EF.Functions.ILike(l.Destination, $"%{term}%") ||
                (l.Title != null && EF.Functions.ILike(l.Title, $"%{term}%")));
        }

        var links = await query.OrderByDescending(l => l.CreatedOnUtc).Take(500).ToListAsync(ct);
        return links.Select(ToDto).ToList();
    }

    public async Task<LinkDto> GetAsync(Guid workspaceId, Guid id, CancellationToken ct)
    {
        await access.RequireLinkAsync(id, AccessLevel.Viewer, ct);
        var link = await FindAsync(workspaceId, id, tracking: false, ct);
        return ToDto(link);
    }

    public async Task<LinkDto> CreateAsync(Guid workspaceId, CreateLinkRequest req, CancellationToken ct)
    {
        await access.RequireWorkspaceAsync(workspaceId, AccessLevel.Editor, ct);
        await EnsureFolderInWorkspaceAsync(workspaceId, req.FolderId, ct);

        var code = await ResolveCodeAsync(req.Code, ct);
        var link = Link.Create(workspaceId, req.FolderId, code, req.Destination, user.RequireOid());
        link.UpdateMetadata(req.Title, req.Notes, req.Tags);
        if (req.ExpiresOnUtc is not null)
        {
            link.SetExpiry(req.ExpiresOnUtc);
        }

        db.Links.Add(link);
        db.LinkEvents.Add(Record(link.Id, LinkEventType.Created, newValue: link.Destination));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (string.IsNullOrWhiteSpace(req.Code))
        {
            // A rare race lost the unique-index insert for a random code — ask the caller to retry.
            throw new DomainException("Code collision on save, please retry.");
        }

        // Clear any negative-cache entry from an earlier miss on this code (before it existed).
        await cache.InvalidateAsync(link.Code, ct);
        return ToDto(link);
    }

    public async Task<LinkDto> UpdateAsync(Guid workspaceId, Guid id, UpdateLinkRequest req, CancellationToken ct)
    {
        await access.RequireLinkAsync(id, AccessLevel.Editor, ct);
        await EnsureFolderInWorkspaceAsync(workspaceId, req.FolderId, ct);

        var link = await FindAsync(workspaceId, id, tracking: true, ct);

        // Snapshot before mutation so we can record only the fields that actually changed.
        var before = (link.Destination, link.FolderId, link.Title, link.Notes, Tags: string.Join(", ", link.Tags), link.ExpiresOnUtc);

        link.UpdateDestination(req.Destination);
        link.MoveToFolder(req.FolderId);
        link.UpdateMetadata(req.Title, req.Notes, req.Tags);
        link.SetExpiry(req.ExpiresOnUtc);

        var events = new List<LinkEvent>();
        if (before.Destination != link.Destination)
        {
            events.Add(Record(id, LinkEventType.DestinationChanged, before.Destination, link.Destination));
        }

        if (before.FolderId != link.FolderId)
        {
            events.Add(Record(id, LinkEventType.FolderMoved,
                await FolderNameAsync(before.FolderId, ct), await FolderNameAsync(link.FolderId, ct)));
        }

        if (before.Title != link.Title || before.Notes != link.Notes || before.Tags != string.Join(", ", link.Tags))
        {
            events.Add(Record(id, LinkEventType.MetadataChanged, newValue: MetadataSummary(before, link)));
        }

        if (before.ExpiresOnUtc != link.ExpiresOnUtc)
        {
            events.Add(Record(id, LinkEventType.ExpiryChanged, FormatExpiry(before.ExpiresOnUtc), FormatExpiry(link.ExpiresOnUtc)));
        }

        db.LinkEvents.AddRange(events);
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAllAsync(link.Id, link.Code, ct);
        return ToDto(link);
    }

    public async Task<LinkDto> RenameAsync(Guid workspaceId, Guid id, RenameLinkRequest req, CancellationToken ct)
    {
        await access.RequireLinkAsync(id, AccessLevel.Editor, ct);
        var link = await FindAsync(workspaceId, id, tracking: true, ct);

        var newCode = req.Code.Trim();
        ShortCodes.ValidateVanity(newCode);
        if (newCode == link.Code)
        {
            throw new DomainException("That is already this link's current code.");
        }

        if (await IsCodeTakenAsync(newCode, ct))
        {
            throw new DomainException($"The code '{newCode}' is already taken.");
        }

        var oldCode = link.Code;
        link.Rename(newCode);
        db.RetiredCodes.Add(RetiredCode.Create(id, oldCode));
        db.LinkEvents.Add(Record(id, LinkEventType.Renamed, oldCode, newCode));
        await db.SaveChangesAsync(ct);

        // Clear a negative-cache miss on the new code; the old code's cached entry (if any) still
        // resolves correctly as-is, since Destination/IsActive/ExpiresOnUtc are unchanged.
        await cache.InvalidateAsync(newCode, ct);
        return ToDto(link);
    }

    public async Task SetActiveAsync(Guid workspaceId, Guid id, bool isActive, CancellationToken ct)
    {
        await access.RequireLinkAsync(id, AccessLevel.Editor, ct);
        var link = await FindAsync(workspaceId, id, tracking: true, ct);
        if (link.IsActive != isActive)
        {
            link.SetActive(isActive);
            db.LinkEvents.Add(Record(id, isActive ? LinkEventType.Enabled : LinkEventType.Disabled));
            await db.SaveChangesAsync(ct);
            await cache.InvalidateAllAsync(link.Id, link.Code, ct);
        }
    }

    public async Task<IReadOnlyList<LinkEventDto>> ListEventsAsync(Guid workspaceId, Guid id, CancellationToken ct)
    {
        await access.RequireLinkAsync(id, AccessLevel.Viewer, ct);
        _ = await FindAsync(workspaceId, id, tracking: false, ct); // scopes the link to the workspace

        var events = await db.LinkEvents
            .Where(e => e.LinkId == id)
            .OrderByDescending(e => e.CreatedOnUtc)
            .Take(200)
            .ToListAsync(ct);

        return events
            .Select(e => new LinkEventDto(e.Type.ToString(), e.ActorName, e.OldValue, e.NewValue, e.CreatedOnUtc))
            .ToList();
    }

    public async Task DeleteAsync(Guid workspaceId, Guid id, CancellationToken ct)
    {
        await access.RequireLinkAsync(id, AccessLevel.Manager, ct);
        // Soft delete: the link moves to the trash (keeps clicks, events, shares) and stops resolving.
        // A Global Admin can restore it, or purge it for good, from the admin trash view.
        var link = await FindAsync(workspaceId, id, tracking: true, ct);
        link.SoftDelete(user.RequireOid());
        db.LinkEvents.Add(Record(id, LinkEventType.Deleted));
        await db.SaveChangesAsync(ct);
        await cache.InvalidateAllAsync(link.Id, link.Code, ct);
    }

    private async Task<Link> FindAsync(Guid workspaceId, Guid id, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.Links.AsTracking() : db.Links;
        return await query.FirstOrDefaultAsync(l => l.Id == id && l.WorkspaceId == workspaceId, ct)
            ?? throw new NotFoundException("Link not found.");
    }

    private LinkEvent Record(Guid linkId, LinkEventType type, string? oldValue = null, string? newValue = null) =>
        LinkEvent.Record(linkId, type, user.RequireOid(), user.Name, oldValue, newValue);

    private async Task<string> FolderNameAsync(Guid? folderId, CancellationToken ct)
    {
        if (folderId is not { } fid)
        {
            return "workspace root";
        }

        var name = await db.Folders.Where(f => f.Id == fid).Select(f => f.Name).FirstOrDefaultAsync(ct);
        return name ?? "workspace root";
    }

    private static string MetadataSummary(
        (string Destination, Guid? FolderId, string? Title, string? Notes, string Tags, DateTime? ExpiresOnUtc) before, Link after)
    {
        var changed = new List<string>();
        if (before.Title != after.Title)
        {
            changed.Add("title");
        }

        if (before.Notes != after.Notes)
        {
            changed.Add("notes");
        }

        if (before.Tags != string.Join(", ", after.Tags))
        {
            changed.Add("tags");
        }

        return changed.Count > 0 ? string.Join(", ", changed) : "details";
    }

    private static string FormatExpiry(DateTime? expiry) =>
        expiry is { } e ? e.ToString("u", System.Globalization.CultureInfo.InvariantCulture) : "never";

    private async Task EnsureFolderInWorkspaceAsync(Guid workspaceId, Guid? folderId, CancellationToken ct)
    {
        if (folderId is not { } fid)
        {
            return;
        }

        var ok = await db.Folders.AnyAsync(f => f.Id == fid && f.WorkspaceId == workspaceId, ct);
        if (!ok)
        {
            throw new DomainException("The folder does not belong to this workspace.");
        }
    }

    private async Task<string> ResolveCodeAsync(string? vanity, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(vanity))
        {
            ShortCodes.ValidateVanity(vanity);
            if (await IsCodeTakenAsync(vanity, ct))
            {
                throw new DomainException($"The code '{vanity}' is already taken.");
            }

            return vanity;
        }

        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var candidate = ShortCodes.NewCandidate();
            if (!await IsCodeTakenAsync(candidate, ct))
            {
                return candidate;
            }
        }

        throw new DomainException("Could not allocate a unique code, please retry.");
    }

    /// <summary>A code is taken if it's any Link's current code, or any Link's Retired code — retired
    /// codes are permanently reserved and never reusable (see ADR-0004).</summary>
    private async Task<bool> IsCodeTakenAsync(string code, CancellationToken ct) =>
        await db.Links.IgnoreQueryFilters().AnyAsync(l => l.Code == code, ct) ||
        await db.RetiredCodes.AnyAsync(rc => rc.Code == code, ct);

    private LinkDto ToDto(Link l) => new(
        l.Id, l.WorkspaceId, l.FolderId, l.Code, $"{_baseUrl}/{l.Code}", l.Destination,
        l.Title, l.Notes, l.Tags, l.IsActive, l.ExpiresOnUtc, l.ClickCount, l.CreatedOnUtc, l.UpdatedOnUtc);
}

// ---- Endpoints ----
public static class LinkEndpoints
{
    public static void MapLinkEndpoints(this IEndpointRouteBuilder api)
    {
        var group = api.MapGroup("/workspaces/{workspaceId:guid}/links").WithTags("Links");

        group.MapGet("/", (Guid workspaceId, Guid? folderId, string? search, LinkService svc, CancellationToken ct) =>
            svc.ListAsync(workspaceId, folderId, search, ct));
        group.MapPost("/", async (Guid workspaceId, CreateLinkRequest req, LinkService svc, CancellationToken ct) =>
            Results.Ok(await svc.CreateAsync(workspaceId, req, ct)));
        group.MapGet("/{id:guid}", (Guid workspaceId, Guid id, LinkService svc, CancellationToken ct) =>
            svc.GetAsync(workspaceId, id, ct));
        group.MapGet("/{id:guid}/events", (Guid workspaceId, Guid id, LinkService svc, CancellationToken ct) =>
            svc.ListEventsAsync(workspaceId, id, ct));
        group.MapPut("/{id:guid}", async (Guid workspaceId, Guid id, UpdateLinkRequest req, LinkService svc, CancellationToken ct) =>
            Results.Ok(await svc.UpdateAsync(workspaceId, id, req, ct)));
        group.MapPatch("/{id:guid}/active", async (Guid workspaceId, Guid id, SetActiveRequest req, LinkService svc, CancellationToken ct) =>
        {
            await svc.SetActiveAsync(workspaceId, id, req.IsActive, ct);
            return Results.NoContent();
        });
        group.MapPost("/{id:guid}/rename", async (Guid workspaceId, Guid id, RenameLinkRequest req, LinkService svc, CancellationToken ct) =>
            Results.Ok(await svc.RenameAsync(workspaceId, id, req, ct)));
        group.MapDelete("/{id:guid}", async (Guid workspaceId, Guid id, LinkService svc, CancellationToken ct) =>
        {
            await svc.DeleteAsync(workspaceId, id, ct);
            return Results.NoContent();
        });
    }
}

public static class LinkServiceExtensions
{
    public static IServiceCollection AddLinks(this IServiceCollection services) =>
        services.AddScoped<LinkService>();
}
