using Beacon.Api.Common;

namespace Beacon.Api.Domain;

/// <summary>
/// A single short URL: a ShortCode plus its Destination and metadata. Belongs to exactly one
/// Workspace and (optionally) one Folder within it.
/// </summary>
public sealed class Link
{
    private Link() { } // EF

    private Link(Guid workspaceId, Guid? folderId, string code, string destination, string createdByOid)
    {
        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        FolderId = folderId;
        Code = code;
        Destination = destination;
        IsActive = true;
        CreatedByOid = createdByOid;
        CreatedOnUtc = DateTime.UtcNow;
        Tags = [];
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid? FolderId { get; private set; }
    public string Code { get; private set; } = null!;
    public string Destination { get; private set; } = null!;
    public string? Title { get; private set; }
    public string? Notes { get; private set; }
    public List<string> Tags { get; private set; } = [];
    public bool IsActive { get; private set; }
    public DateTime? ExpiresOnUtc { get; private set; }
    public long ClickCount { get; private set; }
    public string CreatedByOid { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }
    public DateTime? UpdatedOnUtc { get; private set; }
    public DateTime? DeletedOnUtc { get; private set; }
    public string? DeletedByOid { get; private set; }

    /// <summary>True when this link is in the trash (soft-deleted) and no longer resolves.</summary>
    public bool IsDeleted => DeletedOnUtc is not null;

    public static Link Create(Guid workspaceId, Guid? folderId, string code, string destination, string createdByOid)
    {
        var normalisedDestination = NormaliseDestination(destination);
        // Code is either a validated vanity string or a generated candidate; both must be well-formed.
        if (!ShortCodes.IsWellFormed(code) || ShortCodes.IsReserved(code))
        {
            throw new DomainException("The short code is not valid.");
        }

        return new Link(workspaceId, folderId, code, normalisedDestination, createdByOid);
    }

    /// <summary>Change the current ShortCode. The caller records the old code as a RetiredCode.</summary>
    public void Rename(string newCode)
    {
        if (!ShortCodes.IsWellFormed(newCode) || ShortCodes.IsReserved(newCode))
        {
            throw new DomainException("The short code is not valid.");
        }

        Code = newCode;
        Touch();
    }

    public void UpdateDestination(string destination)
    {
        Destination = NormaliseDestination(destination);
        Touch();
    }

    public void UpdateMetadata(string? title, string? notes, IEnumerable<string>? tags)
    {
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        Tags = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).Distinct().ToList() ?? [];
        Touch();
    }

    public void MoveToFolder(Guid? folderId)
    {
        FolderId = folderId;
        Touch();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        Touch();
    }

    public void SetExpiry(DateTime? expiresOnUtc)
    {
        if (expiresOnUtc is { } e && e <= DateTime.UtcNow)
        {
            throw new DomainException("The expiry must be in the future.");
        }

        ExpiresOnUtc = expiresOnUtc;
        Touch();
    }

    public void RecordClick() => ClickCount++;

    /// <summary>Move to the trash: it stops resolving but keeps its clicks, events, and shares.</summary>
    public void SoftDelete(string byOid)
    {
        DeletedOnUtc = DateTime.UtcNow;
        DeletedByOid = byOid;
        Touch();
    }

    /// <summary>Bring a trashed link back.</summary>
    public void Restore()
    {
        DeletedOnUtc = null;
        DeletedByOid = null;
        Touch();
    }

    /// <summary>True when the code should resolve right now (active and not expired).</summary>
    public bool IsResolvable(DateTime nowUtc) => IsActive && (ExpiresOnUtc is null || ExpiresOnUtc > nowUtc);

    private void Touch() => UpdatedOnUtc = DateTime.UtcNow;

    private static string NormaliseDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new DomainException("A destination URL is required.");
        }

        var trimmed = destination.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new DomainException("The destination must be an absolute http(s) URL.");
        }

        return uri.ToString();
    }
}
