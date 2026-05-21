namespace ObfusCal.Application.Configuration;

public sealed class SecurityAuditOptions
{
	public const string SectionName = "SecurityAudit";

	public string? FilePath { get; set; }

	public string ResolveFilePath()
	{
		if (!string.IsNullOrWhiteSpace(FilePath))
		{
			var trimmedPath = FilePath.Trim();
			// Sanitize path to prevent traversal attacks
			if (trimmedPath.Contains("..") || trimmedPath.Contains("../") || trimmedPath.Contains(@"\.."))
				throw new InvalidOperationException("FilePath cannot contain '..' sequences to prevent path traversal attacks.");

			return trimmedPath;
		}

		return Path.Join(Path.GetTempPath(), "ObfusCal", "security-audit.ndjson");
	}
}

