using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using UAParser;
using Beacon.Api.Domain;
using Beacon.Api.Infrastructure.Persistence;

namespace Beacon.Api.Infrastructure.Analytics;

/// <summary>Raw click data captured at redirect time and handed to the background writer.</summary>
public sealed record ClickEvent(
    Guid LinkId,
    string Code,
    DateTime TimestampUtc,
    string? Referrer,
    string? UserAgent,
    string? IpAddress,
    string? Country);

/// <summary>
/// Bounded, non-blocking buffer between the redirect hot-path and the DB writer. When full it drops
/// the newest event rather than slowing a redirect — analytics is best-effort, redirects are not.
/// </summary>
public sealed class ClickBuffer
{
    private const int Capacity = 10_000;

    private readonly Channel<ClickEvent> _channel = Channel.CreateBounded<ClickEvent>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,
            SingleWriter = false,
        });

    public ChannelReader<ClickEvent> Reader => _channel.Reader;

    public bool TryEnqueue(ClickEvent click) => _channel.Writer.TryWrite(click);
}

/// <summary>
/// Drains the <see cref="ClickBuffer"/>, parses the user-agent, hashes the IP, and persists Click
/// rows in batches, bumping each Link's denormalised click count.
/// </summary>
public sealed partial class ClickWriter(
    ClickBuffer buffer,
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<ClickWriter> logger) : BackgroundService
{
    private const int MaxBatch = 200;
    private static readonly Parser UaParser = Parser.GetDefault();
    private readonly string _ipSalt = config["Analytics:IpHashSalt"] ?? Guid.NewGuid().ToString("N");

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = buffer.Reader;
        while (await reader.WaitToReadAsync(stoppingToken))
        {
            var batch = new List<ClickEvent>(MaxBatch);
            while (batch.Count < MaxBatch && reader.TryRead(out var evt))
            {
                batch.Add(evt);
            }

            if (batch.Count > 0)
            {
                await FlushAsync(batch, stoppingToken);
            }
        }
    }

    private async Task FlushAsync(List<ClickEvent> batch, CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            foreach (var e in batch)
            {
                db.Clicks.Add(ToClick(e));
            }

            foreach (var group in batch.GroupBy(e => e.LinkId))
            {
                var increment = group.Count();
                await db.Links.Where(l => l.Id == group.Key)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.ClickCount, l => l.ClickCount + increment), ct);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogBatchFailed(logger, ex, batch.Count);
        }
    }

    private Click ToClick(ClickEvent e)
    {
        var click = new Click(e.LinkId, e.Code, e.TimestampUtc) { Referrer = e.Referrer, Country = e.Country };

        if (!string.IsNullOrWhiteSpace(e.UserAgent))
        {
            var info = UaParser.Parse(e.UserAgent);
            click.Browser = info.UA.Family;
            click.Os = info.OS.Family;
            click.DeviceType = info.Device.Family;
        }

        if (!string.IsNullOrWhiteSpace(e.IpAddress))
        {
            click.IpHash = Hash(e.IpAddress);
        }

        return click;
    }

    private string Hash(string ip)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(_ipSalt + ip));
        return Convert.ToHexStringLower(bytes);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to persist a batch of {Count} clicks")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, int count);
}

public static class ClickAnalyticsSetup
{
    public static IServiceCollection AddClickAnalytics(this IServiceCollection services)
    {
        services.AddSingleton<ClickBuffer>();
        services.AddHostedService<ClickWriter>();
        return services;
    }
}
