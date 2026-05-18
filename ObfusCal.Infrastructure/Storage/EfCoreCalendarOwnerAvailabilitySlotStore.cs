using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using CoreBusySlot = ObfusCal.Domain.Models.BusySlot;

namespace ObfusCal.Infrastructure.Storage;

public sealed class EfCoreCalendarOwnerAvailabilitySlotStore(AppDbContext dbContext) : ICalendarOwnerAvailabilitySlotStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CoreBusySlot>> GetSlotsAsync(
        Guid calendarOwnerId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken ct = default)
    {
        var entities = await dbContext.CalendarOwnerAvailabilitySlots
            .AsNoTracking()
            .Where(slot => slot.CalendarOwnerId == calendarOwnerId)
            .Where(slot => slot.Start < to && slot.End > from)
            .OrderBy(slot => slot.Start)
            .ToListAsync(ct);

        return entities.Select(slot => new CoreBusySlot(
                slot.SourceEventId,
                slot.Start,
                slot.End,
                slot.Title,
                slot.Description,
                slot.AttendeeEmails,
                slot.Location,
                DeserializeSourceSlots(slot.SourceSlotsJson)))
            .ToList();
    }

    private static IReadOnlyList<CoreBusySlot>? DeserializeSourceSlots(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var sourceSlots = JsonSerializer.Deserialize<SourceSlotDto[]>(json, JsonOptions);
            if (sourceSlots is null || sourceSlots.Length == 0)
                return null;

            return sourceSlots
                .Select(dto => new CoreBusySlot(
                    dto.SourceEventId ?? "merged",
                    dto.Start,
                    dto.End,
                    dto.Title,
                    dto.Description,
                    dto.AttendeeEmails,
                    dto.Location))
                .ToList();
        }
        catch
        {
            // If deserialization fails, return null rather than crashing
            return null;
        }
    }

    /// <summary>
    /// DTO for deserializing source slot JSON. Mirrors the obfuscated slot data structure.
    /// </summary>
    private sealed record SourceSlotDto(
        string? SourceEventId,
        DateTimeOffset Start,
        DateTimeOffset End,
        string? Title,
        string? Description,
        IReadOnlyList<string>? AttendeeEmails,
        string? Location
    );
}



