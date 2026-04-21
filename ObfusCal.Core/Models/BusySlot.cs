namespace ObfusCal.Core.Models;

public record BusySlot(
    string SourceEventId,
    DateTimeOffset Start,
    DateTimeOffset End
);