using ObfusCal.Domain.Models;

namespace ObfusCal.Application.UseCases.GetMergedFreeBusy;

public record MergedFreeBusyResponse(
	DateTimeOffset Start,
	DateTimeOffset End,
	string? Title = null,
	string? Description = null,
	IReadOnlyList<string>? AttendeeEmails = null,
	string? Location = null,
	IReadOnlyList<BusySlot>? SourceSlots = null
);

