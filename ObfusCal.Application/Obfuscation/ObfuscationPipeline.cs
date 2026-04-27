using Microsoft.Extensions.Logging;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation;

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
        ObfuscationAuditContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consultantId);

        var inputEvents = events as IReadOnlyCollection<CalendarEvent> ?? events.ToArray();
        var eventTransformerNames = _transformers
            .Select(transformer => transformer.GetType().Name)
            .ToArray();
        var slotTransformerNames = _slotTransformers
            .Select(transformer => transformer.GetType().Name)
            .ToArray();
        var transformersApplied = eventTransformerNames
            .Concat(slotTransformerNames)
            .ToArray();

        var slots = inputEvents
            .Select(calendarEvent => _transformers.Aggregate(calendarEvent, (current, transformer) => transformer.Transform(current)))
            .Select(calendarEvent => new BusySlot(calendarEvent.Id, calendarEvent.Start, calendarEvent.End))
            .ToList();

        // Apply slot transformers (e.g., merging)
        var finalSlots = _slotTransformers.Aggregate((IReadOnlyList<BusySlot>)slots, (current, transformer) => transformer.Transform(current));
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
}

