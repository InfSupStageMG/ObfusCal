using Microsoft.EntityFrameworkCore;

namespace ObfusCal.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<CalendarOwner> CalendarOwners => Set<CalendarOwner>();
    public DbSet<PeerConnection> PeerConnections => Set<PeerConnection>();
    public DbSet<CalendarOwnerPeerMapping> CalendarOwnerPeerMappings => Set<CalendarOwnerPeerMapping>();
    public DbSet<BusySlot> BusySlots => Set<BusySlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarOwner>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired();
        });

        modelBuilder.Entity<PeerConnection>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.InstanceId).IsRequired();
            e.Property(p => p.BaseAddress).IsRequired();
        });

        modelBuilder.Entity<CalendarOwnerPeerMapping>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasOne(m => m.CalendarOwner)
                .WithMany(c => c.PeerMappings)
                .HasForeignKey(m => m.CalendarOwnerId);
            e.HasOne(m => m.PeerConnection)
                .WithMany(pc => pc.CalendarOwnerMappings)
                .HasForeignKey(m => m.PeerConnectionId);
        });

        modelBuilder.Entity<BusySlot>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.PeerId).IsRequired();
            e.Property(b => b.SourceEventId).IsRequired();
            e.HasIndex(b => b.PeerId);
        });
    }
}

