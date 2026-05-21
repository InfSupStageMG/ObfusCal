using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class FileSecurityAuditService(
	IOptions<SecurityAuditOptions> options,
	ILogRedactor logRedactor,
	ILogger<FileSecurityAuditService> logger) : ISecurityAuditService, IDisposable
{
	private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly string _filePath = options.Value.ResolveFilePath();
	private string? _previousEntryHash = TryReadPreviousEntryHash(options.Value.ResolveFilePath());

	public async Task WriteAsync(SecurityAuditEvent auditEvent, CancellationToken ct = default)
	{
		ArgumentNullException.ThrowIfNull(auditEvent);
		EnsureFileReady();

		var sanitizedEvent = Sanitize(auditEvent);
		var timestampUtc = DateTimeOffset.UtcNow;

		await _gate.WaitAsync(ct);
		try
		{
			var hashInput = new HashableSecurityAuditEntry(
				timestampUtc,
				sanitizedEvent.EventCode,
				sanitizedEvent.ActorIdentity,
				sanitizedEvent.TargetResource,
				sanitizedEvent.TargetId,
				sanitizedEvent.Outcome,
				sanitizedEvent.CorrelationId,
				sanitizedEvent.Metadata,
				_previousEntryHash);

			var entryHash = ComputeHash(hashInput);
			var persistedEntry = new PersistedSecurityAuditEntry(
				hashInput.TimestampUtc,
				hashInput.EventCode,
				hashInput.ActorIdentity,
				hashInput.TargetResource,
				hashInput.TargetId,
				hashInput.Outcome,
				hashInput.CorrelationId,
				hashInput.Metadata,
				hashInput.PreviousEntryHash,
				entryHash);

			var line = JsonSerializer.Serialize(persistedEntry, SerializerOptions) + Environment.NewLine;
			await File.AppendAllTextAsync(_filePath, line, Encoding.UTF8, ct);
			_previousEntryHash = entryHash;
		}
		catch (Exception ex)
		{
			logger.LogError(ex,
				"Failed to append security audit event {EventCode} to dedicated sink {AuditFilePath}.",
				auditEvent.EventCode,
				_filePath);
			throw;
		}
		finally
		{
			_gate.Release();
		}
	}

	public void Dispose() => _gate.Dispose();

	private SecurityAuditEvent Sanitize(SecurityAuditEvent auditEvent)
	{
		var sanitizedMetadata = auditEvent.Metadata?
			.Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
			.OrderBy(pair => pair.Key, StringComparer.Ordinal)
			.ToDictionary(
				pair => SanitizeValue(pair.Key),
				pair => pair.Value is null ? null : SanitizeValue(pair.Value),
				StringComparer.Ordinal);

		return auditEvent with
		{
			EventCode = SanitizeValue(auditEvent.EventCode),
			Outcome = SanitizeValue(auditEvent.Outcome),
			ActorIdentity = SanitizeValue(auditEvent.ActorIdentity),
			TargetResource = SanitizeValue(auditEvent.TargetResource),
			TargetId = auditEvent.TargetId is null ? null : SanitizeValue(auditEvent.TargetId),
			CorrelationId = SanitizeValue(auditEvent.CorrelationId),
			Metadata = sanitizedMetadata
		};
	}

	private string SanitizeValue(string value)
	{
		var singleLine = value.ReplaceLineEndings(" ").Trim();
		var redacted = logRedactor.Redact(singleLine);
		return redacted.Length <= 256 ? redacted : redacted[..256];
	}

	private static string ComputeHash(HashableSecurityAuditEntry entry)
	{
		var payload = JsonSerializer.Serialize(entry, SerializerOptions);
		var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
		return Convert.ToHexString(hashBytes).ToLowerInvariant();
	}

	private static string? TryReadPreviousEntryHash(string filePath)
	{
		if (!File.Exists(filePath))
			return null;

		var lastLine = File.ReadLines(filePath)
			.LastOrDefault(line => !string.IsNullOrWhiteSpace(line));

		if (string.IsNullOrWhiteSpace(lastLine))
			return null;

		try
		{
			using var json = JsonDocument.Parse(lastLine);
			return json.RootElement.TryGetProperty("entryHash", out var hash)
				? hash.GetString()
				: null;
		}
		catch (JsonException)
		{
			return null;
		}
	}

	private void EnsureFileReady()
	{
		var directory = Path.GetDirectoryName(_filePath);
		if (!string.IsNullOrWhiteSpace(directory))
			Directory.CreateDirectory(directory);

		if (!File.Exists(_filePath))
		{
			using var _ = new FileStream(_filePath, FileMode.CreateNew, FileAccess.Write, FileShare.ReadWrite);
		}
	}

	private sealed record HashableSecurityAuditEntry(
		DateTimeOffset TimestampUtc,
		string EventCode,
		string ActorIdentity,
		string TargetResource,
		string? TargetId,
		string Outcome,
		string CorrelationId,
		IReadOnlyDictionary<string, string?>? Metadata,
		string? PreviousEntryHash);

	private sealed record PersistedSecurityAuditEntry(
		DateTimeOffset TimestampUtc,
		string EventCode,
		string ActorIdentity,
		string TargetResource,
		string? TargetId,
		string Outcome,
		string CorrelationId,
		IReadOnlyDictionary<string, string?>? Metadata,
		string? PreviousEntryHash,
		string EntryHash);
}


