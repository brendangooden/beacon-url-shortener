namespace Beacon.Api.Domain;

/// <summary>
/// One resolution of a ShortCode — the analytics unit, recorded at redirect time.
/// The raw IP is never stored; only a salted hash, for coarse unique-visitor counting.
/// </summary>
public sealed class Click
{
    private Click() { } // EF

    public Click(Guid linkId, string code, DateTime timestampUtc)
    {
        Id = Guid.NewGuid();
        LinkId = linkId;
        Code = code;
        TimestampUtc = timestampUtc;
    }

    public Guid Id { get; private set; }
    public Guid LinkId { get; private set; }
    public string Code { get; private set; } = null!;
    public DateTime TimestampUtc { get; private set; }
    public string? Referrer { get; set; }
    public string? Browser { get; set; }
    public string? Os { get; set; }
    public string? DeviceType { get; set; }
    public string? Country { get; set; }
    public string? IpHash { get; set; }
}
