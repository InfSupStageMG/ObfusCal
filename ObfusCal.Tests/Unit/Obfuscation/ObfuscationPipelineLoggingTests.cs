using Microsoft.Extensions.Logging;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;
using ObfusCal.Domain.Obfuscation.Transformers;

namespace ObfusCal.Tests.Unit.Obfuscation;

[TestClass]
public class ObfuscationPipelineLoggingTests
{
    [TestMethod]
    public void Process_EmitsAuditLog_WithRequiredStructuredFields_AndNoSensitiveFields()
    {
        var capturingLogger = new CapturingLogger<ObfuscationPipeline>();

        var pipeline = new ObfuscationPipeline(
            [new RemoveTitleTransformer(), new RemoveAttendeesTransformer()],
            [new MergeBlocksTransformer()],
            capturingLogger);
        var sensitiveEvent = new CalendarEvent(
            Id: "evt-1",
            Title: "Confidential title",
            Description: "Sensitive description",
            Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails: ["alice@example.com"],
            Location: "Secret room");

        _ = pipeline.Process([sensitiveEvent], "consultant-42", ObfuscationAuditContext.Client);

        var logEntry = capturingLogger.Entries.Single(e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("Obfuscation audit completed", StringComparison.Ordinal));

        Assert.AreEqual("consultant-42", logEntry.State["ConsultantId"]);
        Assert.AreEqual(nameof(ObfuscationAuditContext.Client), logEntry.State["Context"]?.ToString());
        Assert.IsTrue(logEntry.State.ContainsKey("EventCount"), "EventCount structured property should be present");
        Assert.AreEqual(1, logEntry.State["EventCount"], "EventCount should equal the number of processed events");
        Assert.AreEqual(1, logEntry.State["FinalSlotCount"], "FinalSlotCount should equal the number of resulting busy slots");
        Assert.AreEqual(1, logEntry.State["InitialBusySlotCount"], "InitialBusySlotCount should equal the pre-merge slot count");
        Assert.IsTrue(logEntry.State.ContainsKey("Timestamp"), "Timestamp structured property should be present");

        var transformerNames = ((IEnumerable<string>)logEntry.State["TransformersApplied"]!).ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "remove-title",
                "remove-attendees",
                "merge-blocks"
            },
            transformerNames);

        Assert.IsFalse(logEntry.Message.Contains("Confidential title", StringComparison.Ordinal),
            "Rendered message must not contain sensitive title");
        Assert.IsFalse(logEntry.Message.Contains("Sensitive description", StringComparison.Ordinal),
            "Rendered message must not contain sensitive description");
        Assert.IsFalse(logEntry.Message.Contains("alice@example.com", StringComparison.Ordinal),
            "Rendered message must not contain attendee email");
        Assert.IsFalse(logEntry.Message.Contains("Secret room", StringComparison.Ordinal),
            "Rendered message must not contain location");

        var structuredPayload = string.Join(
            " | ",
            logEntry.State.Select(pair => pair.Value switch
            {
                IEnumerable<string> values => string.Join(",", values),
                _ => pair.Value?.ToString() ?? string.Empty
            }));

        Assert.IsFalse(structuredPayload.Contains("Confidential title", StringComparison.Ordinal));
        Assert.IsFalse(structuredPayload.Contains("Sensitive description", StringComparison.Ordinal));
        Assert.IsFalse(structuredPayload.Contains("alice@example.com", StringComparison.Ordinal));
        Assert.IsFalse(structuredPayload.Contains("Secret room", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Process_WithEmptyEventList_StillEmitsAuditLog()
    {
        var capturingLogger = new CapturingLogger<ObfuscationPipeline>();
        var pipeline = new ObfuscationPipeline([], [], capturingLogger);

        var busySlots = pipeline.Process([], "consultant-empty", ObfuscationAuditContext.Internal);

        Assert.IsEmpty(busySlots);

        var logEntry = capturingLogger.Entries.Single(e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("Obfuscation audit completed", StringComparison.Ordinal));

        Assert.AreEqual(0, logEntry.State["EventCount"]);
        Assert.AreEqual(0, logEntry.State["FinalSlotCount"]);
        Assert.AreEqual("consultant-empty", logEntry.State["ConsultantId"]);
        Assert.AreEqual(nameof(ObfuscationAuditContext.Internal), logEntry.State["Context"]?.ToString());

        var transformerNames = ((IEnumerable<string>)logEntry.State["TransformersApplied"]!).ToArray();
        Assert.AreEqual(0, transformerNames.Length);

        Assert.IsTrue(
            logEntry.Message.Contains("no transformers configured", StringComparison.Ordinal),
            "When pipeline has no transformers, message must say 'no transformers configured'");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        private readonly Stack<IReadOnlyDictionary<string, object?>> _scopes = [];

        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var properties = new Dictionary<string, object?>();

            foreach (var scope in _scopes.Reverse())
                foreach (var pair in scope)
                    properties[pair.Key] = pair.Value;

            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                foreach (var pair in pairs)
                    if (pair.Key != "{OriginalFormat}")
                        properties[pair.Key] = pair.Value;

            Entries.Add(new LogEntry(logLevel, message, properties));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            var scopeState = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value)
                : new Dictionary<string, object?>();

            _scopes.Push(scopeState);
            return new Scope(_scopes);
        }

        private sealed class Scope(Stack<IReadOnlyDictionary<string, object?>> scopes) : IDisposable
        {
            public void Dispose()
            {
                if (scopes.Count > 0)
                    scopes.Pop();
            }
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> State);
}
