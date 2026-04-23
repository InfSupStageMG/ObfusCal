using ObfusCal.Core.Models;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ObfusCal.Tests.Unit.Obfuscation;[TestClass]
public class ObfuscationPipelineLoggingTests
{
    [TestMethod]
    public void Process_EmitsInformationLog_WithStructuredEventCount_AndNoSensitiveFields()
    {
        var sink = new CollectingSink();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Sink(sink)
            .CreateLogger();

        var pipeline = new ObfuscationPipeline([],[], logger);
        var sensitiveEvent = new CalendarEvent(
            Id: "evt-1",
            Title: "Confidential title",
            Description: "Sensitive description",
            Start: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            End: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            AttendeeEmails:["alice@example.com"],
            Location: "Secret room");

        _ = pipeline.Process([sensitiveEvent]);

        var logEvent = sink.Events.Single(e =>
            e.Level == LogEventLevel.Information
            && e.MessageTemplate.Text.Contains("Processed {EventCount} events through obfuscation pipeline", StringComparison.Ordinal));

        Assert.IsTrue(logEvent.Properties.ContainsKey("EventCount"));
        Assert.AreEqual("1", logEvent.Properties["EventCount"].ToString());

        var rendered = logEvent.RenderMessage();
        Assert.IsFalse(rendered.Contains("Confidential title", StringComparison.Ordinal));
        Assert.IsFalse(rendered.Contains("alice@example.com", StringComparison.Ordinal));
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } =[];
        void ILogEventSink.Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
