namespace ObfusCal.Application.Configuration;

public sealed class SecurityAuditOptions
{
	public const string SectionName = "SecurityAudit";

	public string? FilePath { get; set; }

	public string ResolveFilePath()
	{
		if (!string.IsNullOrWhiteSpace(FilePath))
			return FilePath.Trim();

		return Path.Combine(Path.GetTempPath(), "ObfusCal", "security-audit.ndjson");
	}
}

