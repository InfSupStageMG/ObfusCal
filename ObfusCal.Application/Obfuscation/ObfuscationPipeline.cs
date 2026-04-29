using Microsoft.Extensions.Logging;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Application.Obfuscation;

public sealed class ObfuscationPipeline(
    IEnumerable<IObfuscationTransformer> transformers,
    IEnumerable<IBusySlotTransformer> slotTransformers,
    ILogger<ObfuscationPipeline> logger)
{
    private readonly IObfuscationTransformer[] _transformers = transformers.ToArray();
    private readonly IBusySlotTransformer[] _slotTransformers = slotTransformers.ToArray();

    public IReadOnlyList<BusySlot> Process(
        IEnumerable<CalendarEvent> events,
        string consultantId,
        ObfuscationAuditContext context,
        ObfuscationProfileSettings? profile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consultantId);

        profile ??= ObfuscationProfileSettings.CreateDefault(context);

        var inputEvents = events as IReadOnlyCollection<CalendarEvent> ?? events.ToArray();
        var activeEventTransformers = ResolveEventTransformers(profile);
        var activeSlotTransformers = ResolveSlotTransformers(profile);

        var eventTransformerNames = activeEventTransformers
            .Select(transformer => transformer.GetType().Name)
            .ToArray();
        var slotTransformerNames = activeSlotTransformers
            .Select(transformer => transformer.GetType().Name)
            .ToArray();
        var transformersApplied = eventTransformerNames
            .Concat(slotTransformerNames)
            .ToArray();

        var slots = inputEvents
            .Select(calendarEvent => activeEventTransformers.Aggregate(calendarEvent, (current, transformer) => transformer.Transform(current)))
            .Select(calendarEvent => new BusySlot(
                calendarEvent.Id,
                calendarEvent.Start,
                calendarEvent.End,
                calendarEvent.Title,
                calendarEvent.Description,
                calendarEvent.AttendeeEmails,
                calendarEvent.Location))
            .ToList();

        // Apply slot transformers (e.g., merging)
        var finalSlots = activeSlotTransformers.Aggregate((IReadOnlyList<BusySlot>)slots, (current, transformer) => transformer.Transform(current));
        var timestamp = DateTimeOffset.UtcNow;
        var transformerSummary = transformersApplied.Length == 0
            ? "no transformers configured"
            : string.Join(" -> ", transformersApplied);

        using var _ = logger.BeginScope(new Dictionary<string, object?>
        {
            ["TransformersApplied"] = transformersApplied,
            ["Timestamp"] = timestamp,
            ["InitialBusySlotCount"] = slots.Count
        });

        logger.LogInformation(
            "Obfuscation audit completed for consultant {ConsultantId} in {Context} context: {EventCount} event(s) -> {FinalSlotCount} busy slot(s). Transformers: {TransformersAppliedSummary}",
            consultantId,
            context,
            inputEvents.Count,
            finalSlots.Count,
            transformerSummary);

        return finalSlots;
    }

    private IObfuscationTransformer[] ResolveEventTransformers(ObfuscationProfileSettings profile)
    {
        var active = new List<IObfuscationTransformer>(_transformers.Length);

        foreach (var transformer in _transformers)
        {
            switch (transformer)
            {
                case RemoveTitleTransformer when !profile.RemoveTitle:
                case RemoveDescriptionTransformer when !profile.RemoveDescription:
                case RemoveLocationTransformer when !profile.RemoveLocation:
                case RemoveAttendeesTransformer when !profile.RemoveAttendees:
                    continue;
                case RoundTimesTransformer when !profile.RoundTimes:
                    continue;
                case RoundTimesTransformer:
                    active.Add(new RoundTimesTransformer(profile.RoundingIntervalMinutes));
                    continue;
                default:
                    active.Add(transformer);
                    continue;
            }
        }

        return active.ToArray();
    }

    private IBusySlotTransformer[] ResolveSlotTransformers(ObfuscationProfileSettings profile) =>
        _slotTransformers
            .Where(transformer => transformer is not MergeBlocksTransformer || profile.MergeBlocks)
            .ToArray();
}

