using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Persistence;

public class AppDbContext : DbContext, IDataProtectionKeyContext
{
    private readonly IColumnEncryptor? _columnEncryptor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IColumnEncryptor? columnEncryptor = null)
        : base(options)
    {
        _columnEncryptor = columnEncryptor;
    }

    public DbSet<CalendarOwner> CalendarOwners => Set<CalendarOwner>();
    public DbSet<CalendarSourceInstance> CalendarSourceInstances => Set<CalendarSourceInstance>();
    public DbSet<ObfuscationProfile> ObfuscationProfiles => Set<ObfuscationProfile>();
    public DbSet<PeerConnection> PeerConnections => Set<PeerConnection>();
    public DbSet<CalendarOwnerPeerMapping> CalendarOwnerPeerMappings => Set<CalendarOwnerPeerMapping>();
    public DbSet<CalendarOwnerICalFeed> CalendarOwnerICalFeeds => Set<CalendarOwnerICalFeed>();
    public DbSet<BusySlot> BusySlots => Set<BusySlot>();
    public DbSet<CalendarOwnerAvailabilitySlot> CalendarOwnerAvailabilitySlots => Set<CalendarOwnerAvailabilitySlot>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();
    public DbSet<PluginAllowlistOverride> PluginAllowlistOverrides => Set<PluginAllowlistOverride>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var encryptedString = _columnEncryptor is not null
            ? new EncryptedStringConverter(_columnEncryptor)
            : null;
        var encryptedNullableString = _columnEncryptor is not null
            ? new NullableEncryptedStringConverter(_columnEncryptor)
            : null;

        ConfigureCalendarOwner(modelBuilder);
        ConfigureCalendarSourceInstance(modelBuilder, encryptedNullableString);
        ConfigureObfuscationProfile(modelBuilder);
        ConfigureCalendarOwnerICalFeed(modelBuilder);
        ConfigurePeerConnection(modelBuilder, encryptedString);
        ConfigureCalendarOwnerPeerMapping(modelBuilder);
        ConfigureBusySlot(modelBuilder);
        ConfigureCalendarOwnerAvailabilitySlot(modelBuilder);
        ConfigurePluginAllowlistOverride(modelBuilder);
    }

    private static void ConfigureCalendarOwner(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarOwner>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Name).IsRequired();
            e.Property(c => c.EntraObjectId)
                .HasMaxLength(64);
            e.Property(c => c.CalendarSourcePluginId)
                .HasMaxLength(128);
            e.Property(c => c.ICloudCalendarUrl)
                .HasMaxLength(2048);
            e.Property(c => c.ICloudAppleIdProtected)
                .HasMaxLength(8192);
            e.Property(c => c.ICloudAppSpecificPasswordProtected)
                .HasMaxLength(8192);
            e.Property(c => c.GraphAccessTokenProtected)
                .HasMaxLength(8192);
            e.Property(c => c.GraphRefreshTokenProtected)
                .HasMaxLength(8192);
            e.Property(c => c.GraphGrantedScopes)
                .HasMaxLength(2048);
            e.HasIndex(c => c.EntraObjectId)
                .IsUnique();
            e.Property(c => c.WriteBackPlaceholderTitle).HasMaxLength(256);

            e.HasMany(c => c.ObfuscationProfiles)
                .WithOne(profile => profile.CalendarOwner)
                .HasForeignKey(profile => profile.CalendarOwnerId);

            e.HasMany(c => c.CalendarSourceInstances)
                .WithOne(instance => instance.CalendarOwner)
                .HasForeignKey(instance => instance.CalendarOwnerId);
        });
    }

    private static void ConfigureCalendarSourceInstance(
        ModelBuilder modelBuilder,
        NullableEncryptedStringConverter? encryptedNullableString)
    {
        modelBuilder.Entity<CalendarSourceInstance>(e =>
        {
            e.HasKey(instance => instance.Id);
            e.Property(instance => instance.PluginId)
                .IsRequired()
                .HasMaxLength(128);
            e.Property(instance => instance.DisplayName)
                .IsRequired()
                .HasMaxLength(256);
            e.Property(instance => instance.ConfigurationJson)
                .HasMaxLength(32768);
            e.Property(instance => instance.SecretDataJson)
                .HasMaxLength(32768);
            if (encryptedNullableString is not null)
                e.Property(instance => instance.SecretDataJson).HasConversion(encryptedNullableString);
            e.HasIndex(instance => instance.CalendarOwnerId);
            e.HasIndex(instance => new { instance.CalendarOwnerId, instance.PluginId });
        });
    }

    private static void ConfigureObfuscationProfile(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ObfuscationProfile>(e =>
        {
            e.HasKey(profile => profile.Id);
            e.Property(profile => profile.Context).IsRequired();
            e.Property(profile => profile.RoundingIntervalMinutes).IsRequired();
            e.HasIndex(profile => new { profile.CalendarOwnerId, profile.Context })
                .IsUnique();
        });
    }

    private static void ConfigureCalendarOwnerICalFeed(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarOwnerICalFeed>(e =>
        {
            e.HasKey(f => f.Id);
            e.Property(f => f.FeedUrl).IsRequired().HasMaxLength(2048);
            e.HasOne(f => f.CalendarOwner)
                .WithMany(c => c.ICalFeeds)
                .HasForeignKey(f => f.CalendarOwnerId);
            e.HasIndex(f => f.CalendarOwnerId);
        });
    }

    private static void ConfigurePeerConnection(ModelBuilder modelBuilder, EncryptedStringConverter? encryptedString)
    {
        modelBuilder.Entity<PeerConnection>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.InstanceId).IsRequired();
            e.Property(p => p.BaseAddress).IsRequired();
            e.Property(p => p.PinnedCertificateThumbprint)
                .HasMaxLength(128);
            e.Property(p => p.ClientCertificateThumbprint)
                .HasMaxLength(128);
            e.Property(p => p.ApiKeyHash)
                .IsRequired()
                .HasMaxLength(512);
            if (encryptedString is not null)
                e.Property(p => p.ApiKeyHash).HasConversion(encryptedString);
            e.Property(p => p.Scopes)
                .IsRequired()
                .HasMaxLength(256);
            e.Property(p => p.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            e.Property(p => p.ClientOrganisationName)
                .HasMaxLength(256);
            e.Property(p => p.ClientOrganisationNameNormalized)
                .HasMaxLength(256);

            e.HasOne(p => p.RequestedByCalendarOwner)
                .WithMany()
                .HasForeignKey(p => p.RequestedByCalendarOwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(p => new { p.RequestedByCalendarOwnerId, p.ClientOrganisationNameNormalized })
                .IsUnique();
        });
    }

    private static void ConfigureCalendarOwnerPeerMapping(ModelBuilder modelBuilder)
    {
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
    }

    private static void ConfigureBusySlot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusySlot>(e =>
        {
            e.HasKey(b => b.Id);
            e.Property(b => b.PeerId).IsRequired();
            e.Property(b => b.SourceEventId).IsRequired();
            e.Property(b => b.CreatedAtUtc).IsRequired();
            e.Property(b => b.AttendeeEmails)
                .HasColumnType("text[]");
            e.HasIndex(b => b.PeerId);
            e.HasIndex(b => b.CalendarOwnerId);
            e.HasIndex(b => new { b.PeerId, b.CalendarOwnerId });
            e.HasIndex(b => b.CreatedAtUtc);
        });
    }

    private static void ConfigureCalendarOwnerAvailabilitySlot(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CalendarOwnerAvailabilitySlot>(e =>
        {
            e.HasKey(slot => slot.Id);
            e.Property(slot => slot.SourceEventId).IsRequired();
            e.Property(slot => slot.AttendeeEmails)
                .HasColumnType("text[]");
            e.HasIndex(slot => slot.CalendarOwnerId);
            e.HasIndex(slot => new { slot.CalendarOwnerId, slot.Start, slot.End });
        });
    }

    private static void ConfigurePluginAllowlistOverride(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PluginAllowlistOverride>(e =>
        {
            e.HasKey(o => o.PluginId);
            e.Property(o => o.PluginId).HasMaxLength(128).IsRequired();
            e.Property(o => o.IsEnabled).IsRequired();
            e.Property(o => o.UpdatedAtUtc).IsRequired();
        });
    }
}

