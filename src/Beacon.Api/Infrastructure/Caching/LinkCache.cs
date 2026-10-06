using Microsoft.EntityFrameworkCore;
using Beacon.Api.Infrastructure.Persistence;
using ZiggyCreatures.Caching.Fusion;

namespace Beacon.Api.Infrastructure.Caching;

/// <summary>
/// The state the redirect hot-path needs to decide a code, cached in FusionCache (L1 memory + L2
/// Redis + backplane). Resolvability (active + not expired) is computed at request time from the
/// cached fields, so an expiry that passes while cached still 404s without a fresh DB read.
/// </summary>
public sealed record LinkCacheEntry(Guid LinkId, string Destination, bool IsActive, DateTime? ExpiresOnUtc);

/// <summary>
/// Reads (and invalidates) the code -> target mapping through FusionCache. A missing code is
/// negative-cached briefly so bogus codes cannot hammer the database.
/// </summary>
public sealed class LinkCache(IFusionCache cache, AppDbContext db)
{
    private static readonly TimeSpan NegativeDuration = TimeSpan.FromSeconds(30);

    public static string Key(string code) => $"link:{code}";

    /// <summary>Resolve a code to its cached target, or null when no such code exists.</summary>
    public ValueTask<LinkCacheEntry?> GetAsync(string code, CancellationToken ct) =>
        cache.GetOrSetAsync<LinkCacheEntry?>(
            Key(code),
            async (ctx, token) =>
            {
                var entry = await db.Links
                    .Where(l => l.Code == code)
                    .Select(l => new LinkCacheEntry(l.Id, l.Destination, l.IsActive, l.ExpiresOnUtc))
                    .FirstOrDefaultAsync(token);

                // Not a current code — it may be a RetiredCode, which resolves the same way, straight
                // to the Link's current Destination (never a hop through the new code, see ADR-0004).
                entry ??= await db.RetiredCodes
                    .Where(rc => rc.Code == code)
                    .Join(db.Links, rc => rc.LinkId, l => l.Id,
                        (rc, l) => new LinkCacheEntry(l.Id, l.Destination, l.IsActive, l.ExpiresOnUtc))
                    .FirstOrDefaultAsync(token);

                if (entry is null)
                {
                    // Shorter TTL for a miss — don't pin a bogus code for the full positive duration.
                    ctx.Options.SetDuration(NegativeDuration);
                }

                return entry;
            },
            token: ct);

    /// <summary>Drop the cached entry for a code across all nodes (via the backplane).</summary>
    public ValueTask InvalidateAsync(string code, CancellationToken ct = default) =>
        cache.RemoveAsync(Key(code), token: ct);

    /// <summary>
    /// Drop the cached entries for a Link's current code and every RetiredCode it has. Needed
    /// whenever Destination/IsActive/ExpiresOnUtc changes, since a RetiredCode's cached entry
    /// mirrors those fields and would otherwise go stale until its TTL naturally expires.
    /// </summary>
    public async Task InvalidateAllAsync(Guid linkId, string currentCode, CancellationToken ct = default)
    {
        await InvalidateAsync(currentCode, ct);
        var retiredCodes = await db.RetiredCodes.Where(rc => rc.LinkId == linkId).Select(rc => rc.Code).ToListAsync(ct);
        foreach (var code in retiredCodes)
        {
            await InvalidateAsync(code, ct);
        }
    }
}
