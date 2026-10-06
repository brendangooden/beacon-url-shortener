namespace Beacon.Api.Domain;

/// <summary>The kind of change recorded against a <see cref="Link"/> for its audit trail.</summary>
public enum LinkEventType
{
    Created,
    DestinationChanged,
    FolderMoved,
    MetadataChanged,
    Enabled,
    Disabled,
    ExpiryChanged,
    Deleted,
    Restored,
    Renamed,
}

/// <summary>
/// One entry in a link's audit trail: who changed what, and when. Old/new values are short,
/// human-readable strings (a URL, a folder name, a field summary) — not a full field diff.
/// </summary>
public sealed class LinkEvent
{
    private LinkEvent() { } // EF

    private LinkEvent(Guid linkId, LinkEventType type, string actorOid, string actorName, string? oldValue, string? newValue)
    {
        Id = Guid.NewGuid();
        LinkId = linkId;
        Type = type;
        ActorOid = actorOid;
        ActorName = actorName;
        OldValue = oldValue;
        NewValue = newValue;
        CreatedOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid LinkId { get; private set; }
    public LinkEventType Type { get; private set; }
    public string ActorOid { get; private set; } = null!;
    public string ActorName { get; private set; } = null!;
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public DateTime CreatedOnUtc { get; private set; }

    public static LinkEvent Record(
        Guid linkId, LinkEventType type, string actorOid, string actorName,
        string? oldValue = null, string? newValue = null) =>
        new(linkId, type, actorOid, string.IsNullOrWhiteSpace(actorName) ? actorOid : actorName, Trim(oldValue), Trim(newValue));

    private static string? Trim(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var t = value.Trim();
        return t.Length > 300 ? t[..300] : t;
    }
}
