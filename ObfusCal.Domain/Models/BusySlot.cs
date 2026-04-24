namespace ObfusCal.Domain.Models;

public record BusySlot(
    string SourceEventId,
    DateTimeOffset Start,
    DateTimeOffset End
);

