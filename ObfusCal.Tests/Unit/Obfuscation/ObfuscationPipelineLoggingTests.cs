using Microsoft.Extensions.Logging;
using ObfusCal.Application.Obfuscation;
using ObfusCal.Domain.Models;

namespace ObfusCal.Tests.Unit.Obfuscation;

[TestClass]
public class ObfuscationPipelineLoggingTests
{
    [TestMethod]
    public void Process_EmitsInformationLog_WithStructuredEventCount_AndNoSensitiveFields()
    {
        var capturingLogger = new CapturingLogger<ObfuscationPipeline>();

        var pipeline = new ObfuscationPipeline([], [], capturingLogger);
        var sensitiveEvent = new CalendarEvent(
            Id: "evt-1",
            Title: "Confidential title",
            Description: "Sensitive description",
            Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails: ["alice@example.com"],
            Location: "Secret room");

        _ = pipeline.Process([sensitiveEvent]);

        var logEntry = capturingLogger.Entries.Single(e =>
            e.Level == LogLevel.Information
            && e.Message.Contains("Processed", StringComparison.Ordinal)
            && e.Message.Contains("events through obfuscation pipeline", StringComparison.Ordinal));

        Assert.IsTrue(logEntry.State.ContainsKey("EventCount"), "EventCount structured property should be present");
        Assert.AreEqual(1, logEntry.State["EventCount"], "EventCount should equal the number of processed events");

        Assert.IsFalse(logEntry.Message.Contains("Confidential title", StringComparison.Ordinal),
            "Rendered message must not contain sensitive title");
        Assert.IsFalse(logEntry.Message.Contains("alice@example.com", StringComparison.Ordinal),
            "Rendered message must not contain attendee email");
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            var properties = new Dictionary<string, object?>();

            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
                foreach (var pair in pairs)
                    if (pair.Key != "{OriginalFormat}")
                        properties[pair.Key] = pair.Value;

            Entries.Add(new LogEntry(logLevel, message, properties));
        }

        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private sealed record LogEntry(
        LogLevel Level,
        string Message,
        IReadOnlyDictionary<string, object?> State);
}
