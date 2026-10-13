using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Oxira.Events.Application.Common;
using Oxira.Events.Domain;

namespace Oxira.Events.Infrastructure.Data;

public class EventsDbContext(DbContextOptions<EventsDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadFile> LeadFiles => Set<LeadFile>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<AlertSubscriber> AlertSubscribers => Set<AlertSubscriber>();
    public DbSet<AlertSent> AlertsSent => Set<AlertSent>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<UsageCounter> UsageCounters => Set<UsageCounter>();
    public DbSet<RenderLog> RenderLogs => Set<RenderLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        static void Source<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> e,
            System.Linq.Expressions.Expression<Func<T, LeadSource?>> nav) where T : class =>
            e.OwnsOne(nav!, s =>
            {
                s.Property(x => x.Source).HasColumnName("SourceName").HasMaxLength(200);
                s.Property(x => x.Medium).HasColumnName("SourceMedium").HasMaxLength(200);
                s.Property(x => x.Campaign).HasColumnName("SourceCampaign").HasMaxLength(200);
                s.Property(x => x.Referrer).HasColumnName("SourceReferrer").HasMaxLength(200);
                s.Property(x => x.Landing).HasColumnName("SourceLanding").HasMaxLength(200);
                s.Property(x => x.Raw).HasColumnName("SourceJson").HasMaxLength(4000);
            });

        b.Entity<Lead>(e =>
        {
            e.ToTable("Leads");
            e.Property(x => x.ContactName).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(160);
            e.Property(x => x.Company).HasMaxLength(160);
            e.Property(x => x.Service).HasMaxLength(160);
            e.Property(x => x.EventName).HasMaxLength(160);
            e.Property(x => x.EventDate).HasMaxLength(80);
            e.Property(x => x.City).HasMaxLength(120);
            e.Property(x => x.StandSize).HasMaxLength(40);
            e.Property(x => x.PreferredContact).HasMaxLength(200);
            e.Property(x => x.Channel).HasMaxLength(20);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.Property(x => x.Page).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.DesignJson).HasMaxLength(4000);
            e.Property(x => x.StaffNote).HasMaxLength(4000);
            e.HasIndex(x => new { x.Status, x.CreatedAt });
            e.HasIndex(x => x.CreatedAt);
            Source(e, x => x.Source);
            e.Navigation(x => x.Source).IsRequired();
            e.HasMany(x => x.Files).WithOne().HasForeignKey(f => f.LeadId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<LeadFile>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Mime).HasMaxLength(60);
            e.Property(x => x.Role).HasMaxLength(20);
        });
        b.Entity<Booking>(e =>
        {
            e.Property(x => x.ContactName).HasMaxLength(120);
            e.Property(x => x.Phone).HasMaxLength(40);
            e.Property(x => x.Email).HasMaxLength(160);
            e.Property(x => x.Company).HasMaxLength(160);
            e.Property(x => x.Channel).HasMaxLength(20);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.Property(x => x.Page).HasMaxLength(300);
            e.Property(x => x.Status).HasMaxLength(20);
            // one booking per slot unless it was cancelled
            e.HasIndex(x => x.StartsAt).IsUnique().HasFilter("[Status] <> 'cancelled'");
            Source(e, x => x.Source);
            e.Navigation(x => x.Source).IsRequired();
        });
        b.Entity<AlertSubscriber>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(160);
            e.Property(x => x.Sectors).HasMaxLength(400);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.Property(x => x.Token).HasMaxLength(32);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.Token).IsUnique();
            Source(e, x => x.Source);
            e.Navigation(x => x.Source).IsRequired();
        });
        b.Entity<AlertSent>(e =>
        {
            e.ToTable("AlertsSent");
            e.Property(x => x.EventKey).HasMaxLength(220);
            e.HasIndex(x => new { x.SubscriberId, x.EventKey, x.Stage }).IsUnique();
            e.HasIndex(x => x.EventKey);
        });
        b.Entity<ChatMessage>(e =>
        {
            e.Property(x => x.SessionId).HasMaxLength(64);
            e.Property(x => x.Role).HasMaxLength(12);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.Property(x => x.Page).HasMaxLength(300);
            e.HasIndex(x => new { x.SessionId, x.Id });
        });
        b.Entity<UsageCounter>(e =>
        {
            e.HasKey(x => new { x.Key, x.Day });
            e.Property(x => x.Key).HasMaxLength(120);
        });
        b.Entity<RenderLog>(e =>
        {
            e.Property(x => x.SessionId).HasMaxLength(64);
            e.Property(x => x.Ip).HasMaxLength(64);
            e.Property(x => x.Lang).HasMaxLength(5);
            e.Property(x => x.ConfigJson).HasMaxLength(2000);
            e.HasIndex(x => x.CreatedAt);
        });
    }
}

/// <summary>Lets `dotnet ef` build the model without starting the API.</summary>
public class DesignTimeFactory : IDesignTimeDbContextFactory<EventsDbContext>
{
    public EventsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<EventsDbContext>()
        .UseSqlServer("Server=localhost;Database=OxiraEvents;Trusted_Connection=True;TrustServerCertificate=True").Options);
}

/// <summary>Applies migrations at startup (or creates the schema when the project has none yet).</summary>
public class DbInitializer(EventsDbContext db, ILogger<DbInitializer> log)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync(ct);
        else await db.Database.EnsureCreatedAsync(ct);
        log.LogInformation("Database ready");
    }
}
