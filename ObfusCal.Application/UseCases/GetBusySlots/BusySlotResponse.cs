namespace ObfusCal.Application.UseCases.GetBusySlots;

public record BusySlotResponse(
	DateTimeOffset Start,
	DateTimeOffset End,
	string? Title = null,
	string? Description = null,
	IReadOnlyList<string>? AttendeeEmails = null,
	string? Location = null);

