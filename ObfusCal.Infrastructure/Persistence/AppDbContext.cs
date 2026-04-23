using Microsoft.EntityFrameworkCore;

namespace ObfusCal.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Consultant> Consultants => Set<Consultant>();
    public DbSet<PeerConnection> PeerConnections => Set<PeerConnection>();
    public DbSet<ConsultantPeerMapping> ConsultantPeerMappings => Set<ConsultantPeerMapping>();
    public DbSet<BusySlot> BusySlots => Set<BusySlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Consultant>(e =>
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

        modelBuilder.Entity<ConsultantPeerMapping>(e =>
        {
            e.HasKey(m => m.Id);
            e.HasOne(m => m.Consultant)
                .WithMany(c => c.PeerMappings)
                .HasForeignKey(m => m.ConsultantId);
            e.HasOne(m => m.PeerConnection)
                .WithMany(pc => pc.ConsultantMappings)
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

