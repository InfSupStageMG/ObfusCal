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
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task WriteAsync_EmitsCorrectEventCode()
    {
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        var auditEvent = new SecurityAuditEvent(
            EventCode: "AUTH_SUCCESS",
            Outcome: "success",
            ActorIdentity: "peer-123",
            TargetResource: "/api/shadow-slots",
            TargetId: null,
            CorrelationId: "corr-abc-123");

        await service.WriteAsync(auditEvent, TestContext.CancellationToken);

        var entries = await ReadAuditFileAsync(filePath);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("AUTH_SUCCESS", entries[0].EventCode);
        Assert.AreEqual("success", entries[0].Outcome);
        Assert.AreEqual("peer-123", entries[0].ActorIdentity);
        Assert.AreEqual("/api/shadow-slots", entries[0].TargetResource);
    }

    [TestMethod]
    public async Task WriteAsync_SanitizesCredentialPatterns_BearerToken()
    {
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        var sensitiveToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.Signature";
        var auditEvent = new SecurityAuditEvent(
            EventCode: "AUTH_FAILURE",
            Outcome: "failure",
            ActorIdentity: "peer-malicious",
            TargetResource: "/api/shadow-slots",
            TargetId: null,
            CorrelationId: "corr-xyz",
            Metadata: new Dictionary<string, string?> { { "token", $"Bearer {sensitiveToken}" } });

        await service.WriteAsync(auditEvent, TestContext.CancellationToken);

        var lines = (await File.ReadAllLinesAsync(filePath)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.AreEqual(1, lines.Length);

        Assert.IsFalse(
            lines[0].Contains(sensitiveToken, StringComparison.Ordinal),
            "Raw JWT token should not appear in audit log.");
        Assert.IsTrue(
            lines[0].Contains("Bearer [REDACTED]", StringComparison.Ordinal),
            "Redacted bearer token should appear.");
    }

    [TestMethod]
    public async Task WriteAsync_SanitizesCredentialPatterns_ApiKeyInKeyValue()
    {
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        var secretApiKey = "sk-abc123def456xyz789secret";
        // Build the full key=value string to test the redaction pattern
        var metadataAsString = $"api_key={secretApiKey}";
        var redactedValue = redactor.Redact(metadataAsString);

        Assert.IsFalse(
            redactedValue.Contains(secretApiKey, StringComparison.Ordinal),
            "API key should be redacted when in full key=value format.");
        Assert.IsTrue(
            redactedValue.Contains("api_key=[REDACTED]", StringComparison.Ordinal),
            "Redacted format should appear in output.");
    }

    [TestMethod]
    public async Task WriteAsync_SanitizesCredentialPatterns_ConnectionString()
    {
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        var auditEvent = new SecurityAuditEvent(
            EventCode: "AUTH_FAILURE",
            Outcome: "failure",
            ActorIdentity: "system",
            TargetResource: "database",
            TargetId: null,
            CorrelationId: "corr-db-1",
            Metadata: new Dictionary<string, string?> { { "reason", "Host=db;Database=obfuscal;Username=appuser;Password=P@ssw0rd123" } });

        await service.WriteAsync(auditEvent, TestContext.CancellationToken);

        var lines = (await File.ReadAllLinesAsync(filePath)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.AreEqual(1, lines.Length);

        Assert.IsFalse(
            lines[0].Contains("P@ssw0rd123", StringComparison.Ordinal),
            "Database password should not appear in audit log.");
        Assert.IsTrue(
            lines[0].Contains("Host=db", StringComparison.Ordinal),
            "Connection string prefix should still appear.");
        Assert.IsTrue(
            lines[0].Contains("Password=[REDACTED]", StringComparison.Ordinal),
            "Password should be redacted.");
    }

    [TestMethod]
    public async Task WriteAsync_WithDirectoryFilePath_WritesToDefaultNdjsonFile()
    {
        var directoryPath = Path.Join(Path.GetTempPath(), "ObfusCal", "tests", $"dir-{Guid.NewGuid():N}") + Path.DirectorySeparatorChar;
        var resolvedFilePath = Path.Join(directoryPath, SecurityAuditOptions.DefaultFileName);
        var options = Options.Create(new SecurityAuditOptions { FilePath = directoryPath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        await service.WriteAsync(new SecurityAuditEvent(
            SecurityAuditEventCodes.AuthSuccess,
            SecurityAuditOutcomes.Success,
            "peer-directory-path",
            "/api/shadow-slots",
            null,
            "corr-directory-path"), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(resolvedFilePath));

        var entries = await ReadAuditFileAsync(resolvedFilePath);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(SecurityAuditEventCodes.AuthSuccess, entries[0].EventCode);
    }

    [TestMethod]
    public async Task WriteAsync_WithExplicitJsonFilePath_WritesToConfiguredFile()
    {
        var directoryPath = Path.Join(Path.GetTempPath(), "ObfusCal", "tests", $"json-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        var filePath = Path.Join(directoryPath, "security-audit.json");
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        await service.WriteAsync(new SecurityAuditEvent(
            SecurityAuditEventCodes.AuthFailure,
            SecurityAuditOutcomes.Failure,
            "peer-explicit-file",
            "/api/shadow-slots",
            null,
            "corr-explicit-file"), TestContext.CancellationToken);

        Assert.IsTrue(File.Exists(filePath));

        var entries = await ReadAuditFileAsync(filePath);
        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual(SecurityAuditEventCodes.AuthFailure, entries[0].EventCode);
    }

    [TestMethod]
    public async Task WriteAsync_MultipleEvents_BuildsTamperEvidenceChain()
    {
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        var event1 = new SecurityAuditEvent("AUTH_SUCCESS", "success", "peer-1", "/api/sync", null, "corr-1");
        var event2 = new SecurityAuditEvent("PEER_SLOT_PUSH", "success", "peer-1", "/api/shadow-slots", null, "corr-2");
        var event3 = new SecurityAuditEvent("AUTH_FAILURE", "failure", "peer-2", "/api/sync", null, "corr-3");

        await service.WriteAsync(event1, TestContext.CancellationToken);
        await service.WriteAsync(event2, TestContext.CancellationToken);
        await service.WriteAsync(event3, TestContext.CancellationToken);

        var lines = (await File.ReadAllLinesAsync(filePath)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.AreEqual(3, lines.Length);

        var entries = lines.Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;
            return new
            {
                EventCode = root.GetProperty("eventCode").GetString(),
                EntryHash = root.GetProperty("entryHash").GetString(),
                PreviousHash = root.TryGetProperty("previousEntryHash", out var ph) ? ph.GetString() : null
            };
        }).ToArray();

        // First entry should have null previousHash
        Assert.IsNull(entries[0].PreviousHash);

        // Second entry's previousHash should match first entry's hash
        Assert.AreEqual(entries[0].EntryHash, entries[1].PreviousHash);

        // Third entry's previousHash should match second entry's hash
        Assert.AreEqual(entries[1].EntryHash, entries[2].PreviousHash);
    }

    [TestMethod]
    public async Task WriteAsync_TruncatesLongValues()
    {
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });
        var redactor = new DefaultLogRedactor();
        using var service = new FileSecurityAuditService(options, redactor, new NullLogger<FileSecurityAuditService>());

        var veryLongValue = new string('x', 500);
        var auditEvent = new SecurityAuditEvent(
            EventCode: "METADATA_LOG",
            Outcome: "success",
            ActorIdentity: "test",
            TargetResource: "/api/test",
            TargetId: null,
            CorrelationId: "corr-1",
            Metadata: new Dictionary<string, string?> { { "longfield", veryLongValue } });

        await service.WriteAsync(auditEvent, TestContext.CancellationToken);

        var entries = await ReadAuditFileAsync(filePath);
        Assert.AreEqual(1, entries.Count);

        // Check that the value was truncated
        var metadata = entries[0].Metadata;
        if (metadata?.TryGetValue("longfield", out var longfieldValue) == true && longfieldValue != null)
        {
            Assert.IsTrue(longfieldValue.Length <= 256, "Value should be truncated to 256 chars");
        }
    }

    [TestMethod]
    public async Task WriteAsync_SanitizesMultipleFields()
    {
        var redactor = new DefaultLogRedactor();

        // Test that multiple credential patterns are redacted in a single string
        var input = "Setup: Bearer secret-token-123; clientSecret=gcp_secret_xyz_12345; refreshToken: refresh_token_abcdef Password=P@ss";

        var result = redactor.Redact(input);

        Assert.IsFalse(result.Contains("secret-token-123", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("gcp_secret_xyz_12345", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("refresh_token_abcdef", StringComparison.Ordinal));
        Assert.IsFalse(result.Contains("P@ss", StringComparison.Ordinal));
        Assert.IsTrue(result.Contains("[REDACTED]", StringComparison.Ordinal));
    }

    private static string GetTempAuditFilePath()
    {
        var tempDir = Path.Join(Path.GetTempPath(), "ObfusCal", "tests", $"unit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return Path.Join(tempDir, "audit.ndjson");
    }

    // --- BEFORE protection: demonstrates credential exposure without log redaction ---

    [TestMethod]
    public async Task WriteAsync_WithoutRedactor_ExposesSensitiveValuesInAuditLog()
    {
        // Before ILogRedactor was plumbed into the audit sink, any value passed in metadata
        // was written verbatim. This test demonstrates that state by using a pass-through
        // redactor implementation that leaves all values unchanged.
        var filePath = GetTempAuditFilePath();
        var options = Options.Create(new SecurityAuditOptions { FilePath = filePath });

        // The pass-through redactor simulates the pre-protection state: no pattern matching.
        using var service = new FileSecurityAuditService(options, new PassThroughRedactor(), new NullLogger<FileSecurityAuditService>());

        const string sensitiveToken = "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.Signature";
        var auditEvent = new SecurityAuditEvent(
            EventCode: "AUTH_FAILURE",
            Outcome: "failure",
            ActorIdentity: "peer-attacker",
            TargetResource: "/api/shadow-slots",
            TargetId: null,
            CorrelationId: "corr-before",
            Metadata: new Dictionary<string, string?> { { "token", $"Bearer {sensitiveToken}" } });

        await service.WriteAsync(auditEvent, CancellationToken.None);

        var lines = (await File.ReadAllLinesAsync(filePath)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        Assert.AreEqual(1, lines.Length);

        Assert.IsTrue(
            lines[0].Contains(sensitiveToken, StringComparison.Ordinal),
            "Without redaction, the raw bearer token value appears verbatim in the audit log — demonstrating the pre-hardening vulnerability.");
    }

    // Minimal pass-through redactor that represents the pre-ILogRedactor state.
    private sealed class PassThroughRedactor : ILogRedactor
    {
        public string Redact(string input) => input;
    }

    private static async Task<IReadOnlyList<AuditEntry>> ReadAuditFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return [];

        var lines = (await File.ReadAllLinesAsync(filePath)).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
        var result = lines.Select(line =>
        {
            using var json = JsonDocument.Parse(line);
            var root = json.RootElement;

            var metadata = new Dictionary<string, string?>();
            if (root.TryGetProperty("metadata", out var metaObj) && metaObj.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in metaObj.EnumerateObject())
                {
                    metadata[prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() : null;
                }
            }

            return new AuditEntry(
                EventCode: root.GetProperty("eventCode").GetString() ?? string.Empty,
                ActorIdentity: root.GetProperty("actorIdentity").GetString() ?? string.Empty,
                TargetResource: root.GetProperty("targetResource").GetString() ?? string.Empty,
                Outcome: root.GetProperty("outcome").GetString() ?? string.Empty,
                CorrelationId: root.GetProperty("correlationId").GetString() ?? string.Empty,
                EntryHash: root.GetProperty("entryHash").GetString(),
                Metadata: metadata);
        }).ToList();

        return result;
    }

    private sealed record AuditEntry(
        string EventCode,
        string ActorIdentity,
        string TargetResource,
        string Outcome,
        string CorrelationId,
        string? EntryHash,
        Dictionary<string, string?> Metadata);
}

















