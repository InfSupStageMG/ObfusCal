namespace ObfusCal.Application.Configuration;

public sealed class SecurityAuditOptions
{
    public const string SectionName = "SecurityAudit";
    public const string DefaultFileName = "security-audit.ndjson";

    public string? FilePath { get; set; }

    public string ResolveFilePath()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
            return Path.Join(Path.GetTempPath(), "ObfusCal", DefaultFileName);
        var trimmedPath = FilePath.Trim();
        if (trimmedPath.Contains("..") || trimmedPath.Contains("../") || trimmedPath.Contains(@"\.."))
            throw new InvalidOperationException(
                "FilePath cannot contain '..' sequences to prevent path traversal attacks.");
        if (Path.EndsInDirectorySeparator(trimmedPath))
            return Path.Join(trimmedPath, DefaultFileName);
        return trimmedPath;
    }
}
