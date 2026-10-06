using Microsoft.EntityFrameworkCore;
using Beacon.Api.Domain;

namespace Beacon.Api.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<ResourceGrant> ResourceGrants => Set<ResourceGrant>();
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Link> Links => Set<Link>();
    public DbSet<RetiredCode> RetiredCodes => Set<RetiredCode>();
    public DbSet<LinkEvent> LinkEvents => Set<LinkEvent>();
    public DbSet<Click> Clicks => Set<Click>();
    public DbSet<Domain.Branding> Branding => Set<Domain.Branding>();

    // Applied at registration (pooling-safe), never in OnConfiguring — Aspire pools the context.
    public static void ConfigureOptions(DbContextOptionsBuilder options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        options.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AppUser>(b =>
        {
            b.HasKey(u => u.Oid);
            b.Property(u => u.Oid).HasMaxLength(100);
            b.Property(u => u.Email).HasMaxLength(320);
            b.Property(u => u.Name).HasMaxLength(200);
        });

        modelBuilder.Entity<Workspace>(b =>
        {
            b.HasKey(w => w.Id);
            b.Property(w => w.Name).HasMaxLength(200).IsRequired();
            b.Property(w => w.CreatedByOid).HasMaxLength(100).IsRequired();
            b.HasIndex(w => w.CreatedByOid);
        });

        modelBuilder.Entity<ResourceGrant>(b =>
        {
            b.HasKey(g => g.Id);
            b.Property(g => g.ResourceType).HasConversion<string>().HasMaxLength(20);
            b.Property(g => g.GranteeOid).HasMaxLength(100).IsRequired();
            b.Property(g => g.GranteeEmail).HasMaxLength(320);
            b.Property(g => g.GranteeName).HasMaxLength(200);
            b.Property(g => g.Level).HasConversion<string>().HasMaxLength(20);
            b.Property(g => g.GrantedByOid).HasMaxLength(100);
            b.HasIndex(g => new { g.ResourceType, g.ResourceId, g.GranteeOid }).IsUnique();
            b.HasIndex(g => g.GranteeOid);
        });

        modelBuilder.Entity<Folder>(b =>
        {
            b.HasKey(f => f.Id);
            b.Property(f => f.Name).HasMaxLength(200).IsRequired();
            b.Property(f => f.CreatedByOid).HasMaxLength(100).IsRequired();
            b.HasIndex(f => f.WorkspaceId);
            b.HasOne<Workspace>().WithMany().HasForeignKey(f => f.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Link>(b =>
        {
            b.HasKey(l => l.Id);
            b.Property(l => l.Code).HasMaxLength(ShortCodeMaxLength).IsRequired();
            b.Property(l => l.Destination).HasMaxLength(2048).IsRequired();
            b.Property(l => l.Title).HasMaxLength(300);
            b.Property(l => l.Notes).HasMaxLength(2000);
            b.Property(l => l.Tags).HasColumnType("text[]");
            b.Property(l => l.DeletedByOid).HasMaxLength(100);
            b.HasIndex(l => l.Code).IsUnique();
            b.HasIndex(l => l.WorkspaceId);
            b.HasIndex(l => l.FolderId);
            b.HasIndex(l => l.DeletedOnUtc);
            // Trashed links are hidden everywhere by default; the admin trash view opts out with
            // IgnoreQueryFilters(). Redirects therefore stop resolving a soft-deleted code too.
            b.HasQueryFilter(l => l.DeletedOnUtc == null);
            b.HasOne<Workspace>().WithMany().HasForeignKey(l => l.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Folder>().WithMany().HasForeignKey(l => l.FolderId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<RetiredCode>(b =>
        {
            b.HasKey(rc => rc.Id);
            b.Property(rc => rc.Code).HasMaxLength(ShortCodeMaxLength).IsRequired();
            b.HasIndex(rc => rc.Code).IsUnique();
            b.HasIndex(rc => rc.LinkId);
            // Cascades on purge (Link.OnDelete Cascade below) so a permanently-deleted link frees
            // every code it ever held, current and retired (see ADR-0004).
            b.HasOne<Link>().WithMany().HasForeignKey(rc => rc.LinkId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Domain.Branding>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.AppName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Tagline).HasMaxLength(200);
            b.Property(x => x.HomeUrl).HasMaxLength(2048);
            b.Property(x => x.PrimaryColor).HasMaxLength(9);
            b.Property(x => x.LogoContentType).HasMaxLength(100);
        });

        modelBuilder.Entity<LinkEvent>(b =>
        {
            b.HasKey(e => e.Id);
            b.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
            b.Property(e => e.ActorOid).HasMaxLength(100).IsRequired();
            b.Property(e => e.ActorName).HasMaxLength(200).IsRequired();
            b.Property(e => e.OldValue).HasMaxLength(300);
            b.Property(e => e.NewValue).HasMaxLength(300);
            b.HasIndex(e => new { e.LinkId, e.CreatedOnUtc });
            b.HasOne<Link>().WithMany().HasForeignKey(e => e.LinkId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Click>(b =>
        {
            b.HasKey(c => c.Id);
            b.Property(c => c.Code).HasMaxLength(ShortCodeMaxLength).IsRequired();
            b.Property(c => c.Referrer).HasMaxLength(2048);
            b.Property(c => c.Browser).HasMaxLength(100);
            b.Property(c => c.Os).HasMaxLength(100);
            b.Property(c => c.DeviceType).HasMaxLength(40);
            b.Property(c => c.Country).HasMaxLength(2);
            b.Property(c => c.IpHash).HasMaxLength(64);
            b.HasIndex(c => new { c.LinkId, c.TimestampUtc });
            b.HasOne<Link>().WithMany().HasForeignKey(c => c.LinkId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private const int ShortCodeMaxLength = 64;
}
