using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class FileSecurityAuditServiceTests
{
    private string _auditFilePath = null!;

    [TestInitialize]
    public void Setup()
    {

        var baseDir = Path.Combine(Path.GetTempPath(), "ObfusCal", "audit-tests");
        var leaf = Guid.NewGuid()
            .ToString("N")
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var dir = Path.Combine(baseDir, leaf);
        Directory.CreateDirectory(dir);
        const string auditFileName = "security-audit.ndjson";
        if (Path.IsPathRooted(auditFileName))
            throw new InvalidOperationException("Audit file name must be a relative path segment.");
        _auditFilePath = Path.Combine(dir, auditFileName);
    }

    [TestCleanup]
    public void Cleanup()
    {
        var dir = Path.GetDirectoryName(_auditFilePath);
        if (dir is not null && Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }

    [TestMethod]
    public async Task WriteAsync_CreatesFile_WhenFileDoesNotExist()
    {
        using var service = CreateService();

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS"));

        Assert.IsTrue(File.Exists(_auditFilePath), "Audit file must be created on first write.");
    }

    [TestMethod]
    public async Task WriteAsync_AppendsOneNdjsonLine_PerEvent()
    {
        using var service = CreateService();

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS"));
        await service.WriteAsync(BuildEvent("AUTH_FAILURE"));
        await service.WriteAsync(BuildEvent("CONFIG_CHANGE"));

        var lines = await ReadNonEmptyLinesAsync();
        Assert.HasCount(3, lines, "Each WriteAsync call must produce exactly one NDJSON line.");
    }

    [TestMethod]
    public async Task WriteAsync_FirstEntry_HasNullPreviousEntryHash()
    {
        using var service = CreateService();

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS"));

        var lines = await ReadNonEmptyLinesAsync();
        var entry = ParseEntry(lines[0]);
        Assert.IsNull(entry.PreviousEntryHash, "The very first entry must have a null previousEntryHash.");
    }

    [TestMethod]
    public async Task WriteAsync_SecondEntry_PreviousHashMatchesFirstEntryHash()
    {
        using var service = CreateService();

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS"));
        await service.WriteAsync(BuildEvent("AUTH_FAILURE"));

        var lines = await ReadNonEmptyLinesAsync();
        var first = ParseEntry(lines[0]);
        var second = ParseEntry(lines[1]);

        Assert.IsNotNull(first.EntryHash, "First entry must have a computed entryHash.");
        Assert.AreEqual(first.EntryHash, second.PreviousEntryHash,
            "Second entry's previousEntryHash must equal the first entry's entryHash.");
    }

    [TestMethod]
    public async Task WriteAsync_HashChain_IsConsistentAcrossAllEntries()
    {
        using var service = CreateService();
        const int entryCount = 5;

        for (var i = 0; i < entryCount; i++)
            await service.WriteAsync(BuildEvent($"EVENT_{i}"));

        var lines = await ReadNonEmptyLinesAsync();
        var entries = lines.Select(ParseEntry).ToArray();

        for (var i = 1; i < entries.Length; i++)
        {
            Assert.AreEqual(entries[i - 1].EntryHash, entries[i].PreviousEntryHash,
                $"Entry {i} previousEntryHash must match entry {i - 1} entryHash.");
        }
    }

    [TestMethod]
    public async Task WriteAsync_EachEntry_HasNonEmptyEntryHash()
    {
        using var service = CreateService();

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS"));

        var entry = ParseEntry((await ReadNonEmptyLinesAsync())[0]);
        Assert.IsFalse(string.IsNullOrWhiteSpace(entry.EntryHash), "Every entry must have a non-empty entryHash.");
    }

    [TestMethod]
    public async Task WriteAsync_TruncatesFieldValues_LongerThan256Characters()
    {
        using var service = CreateService();
        var longValue = new string('x', 300);

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS", actorIdentity: longValue));

        var entry = ParseEntry((await ReadNonEmptyLinesAsync())[0]);
        Assert.IsLessThanOrEqualTo(256, entry.ActorIdentity.Length,
            "Field values longer than 256 characters must be truncated before persisting.");
    }

    [TestMethod]
    public async Task WriteAsync_SanitizesNewlines_InFieldValues()
    {
        using var service = CreateService();
        const string valueWithNewlines = "actor\nidentity\rwith\r\nnewlines";

        await service.WriteAsync(BuildEvent("AUTH_SUCCESS", actorIdentity: valueWithNewlines));

        var lines = await ReadNonEmptyLinesAsync();
        Assert.AreEqual(1, lines.Length, "Log injection via newlines must not split a single event into multiple lines.");
        var entry = ParseEntry(lines[0]);
        Assert.IsFalse(entry.ActorIdentity.Contains('\n'), "Newlines must be stripped from persisted field values.");
        Assert.IsFalse(entry.ActorIdentity.Contains('\r'), "Carriage returns must be stripped from persisted field values.");
    }

    [TestMethod]
    public async Task WriteAsync_RedactsSensitivePatterns_InMetadataValues()
    {
        using var service = CreateService();
        var tokenString = "Bearer " + "test-token-for-redaction-demo";
        var metadata = new Dictionary<string, string?>
        {
            ["reason"] = "auth",
            ["authHeader"] = tokenString
        };

        await service.WriteAsync(new SecurityAuditEvent(
            "AUTH_FAILURE", "FAILURE", "actor", "target", null,
            "correlation-id", metadata));

        var entry = ParseEntry((await ReadNonEmptyLinesAsync())[0]);
        var storedValue = entry.Metadata?["authHeader"];
        Assert.IsFalse(storedValue?.Contains("test-token-for-redaction-demo") ?? false,
            "****** values must be redacted before being written to the audit log.");
        Assert.IsTrue(storedValue?.Contains("[REDACTED]") ?? false,
            "Redacted values must contain the '[REDACTED]' placeholder.");
    }

    [TestMethod]
    public async Task NewServiceInstance_ContinuesHashChain_FromExistingFile()
    {
        // Write two events with the first service instance.
        using (var firstInstance = CreateService())
        {
            await firstInstance.WriteAsync(BuildEvent("AUTH_SUCCESS"));
            await firstInstance.WriteAsync(BuildEvent("CONFIG_CHANGE"));
        }

        // Create a second instance that reads the same file.
        using var secondInstance = CreateService();
        await secondInstance.WriteAsync(BuildEvent("KEY_ROTATION"));

        var lines = await ReadNonEmptyLinesAsync();
        Assert.HasCount(3, lines);

        var entries = lines.Select(ParseEntry).ToArray();
        Assert.AreEqual(entries[1].EntryHash, entries[2].PreviousEntryHash,
            "A new service instance must continue the hash chain from where the previous instance left off.");
    }

    [TestMethod]
    public async Task WriteAsync_ConcurrentWrites_ProduceCorrectLineCount()
    {
        using var service = CreateService();
        const int concurrentWrites = 10;

        await Task.WhenAll(Enumerable.Range(0, concurrentWrites)
            .Select(i => service.WriteAsync(BuildEvent($"CONCURRENT_{i}"))));

        var lines = await ReadNonEmptyLinesAsync();
        Assert.HasCount(concurrentWrites, lines,
            "Concurrent writes must not corrupt the file or drop events.");
    }

    // --- Helpers ---

    private FileSecurityAuditService CreateService() =>
        new(
            Options.Create(new SecurityAuditOptions { FilePath = _auditFilePath }),
            new DefaultLogRedactor(),
            NullLogger<FileSecurityAuditService>.Instance);

    private static SecurityAuditEvent BuildEvent(
        string eventCode,
        string actorIdentity = "test-actor") =>
        new(eventCode, "SUCCESS", actorIdentity, "test-resource", null, Guid.NewGuid().ToString());

    private async Task<string[]> ReadNonEmptyLinesAsync() =>
        (await File.ReadAllTextAsync(_auditFilePath))
        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToArray();

    private static PersistedAuditEntrySnapshot ParseEntry(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        var root = doc.RootElement;

        return new PersistedAuditEntrySnapshot(
            EntryHash: root.GetProperty("entryHash").GetString() ?? string.Empty,
            PreviousEntryHash: root.TryGetProperty("previousEntryHash", out var prev) && prev.ValueKind != JsonValueKind.Null
                ? prev.GetString()
                : null,
            EventCode: root.GetProperty("eventCode").GetString() ?? string.Empty,
            ActorIdentity: root.GetProperty("actorIdentity").GetString() ?? string.Empty,
            Metadata: root.TryGetProperty("metadata", out var meta) && meta.ValueKind == JsonValueKind.Object
                ? meta.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString())
                : null);
    }

    private sealed record PersistedAuditEntrySnapshot(
        string EntryHash,
        string? PreviousEntryHash,
        string EventCode,
        string ActorIdentity,
        IReadOnlyDictionary<string, string?>? Metadata);
}
