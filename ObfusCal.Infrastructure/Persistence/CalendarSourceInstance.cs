namespace ObfusCal.Infrastructure.Persistence;

public class CalendarSourceInstance
{
    public Guid Id { get; set; }
    public Guid CalendarOwnerId { get; set; }
    public required string PluginId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? ConfigurationJson { get; set; }
    public string? SecretDataJson { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }

    public CalendarOwner CalendarOwner { get; set; } = null!;
}

