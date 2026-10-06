namespace Beacon.Api.Domain;

/// <summary>
/// A former ShortCode of a Link, kept after a Rename so it still forwards to the Link's
/// Destination. Permanently reserved: never reusable by any Link, including this one.
/// </summary>
public sealed class RetiredCode
{
    private RetiredCode() { } // EF

    private RetiredCode(Guid linkId, string code)
    {
        Id = Guid.NewGuid();
        LinkId = linkId;
        Code = code;
        CreatedOnUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid LinkId { get; private set; }
    public string Code { get; private set; } = null!;
    public DateTime CreatedOnUtc { get; private set; }

    public static RetiredCode Create(Guid linkId, string code) => new(linkId, code);
}
