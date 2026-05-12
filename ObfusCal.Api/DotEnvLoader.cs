/// <summary>
/// Minimal .env file loader. Reads a file of KEY=VALUE lines and injects each entry
/// into the current process environment so that EnvironmentSecretProvider and
/// IConfiguration can resolve them — identical to what docker-compose does automatically.
///
/// Rules:
/// - Lines starting with # are comments and are skipped.
/// - Empty lines are skipped.
/// - Inline comments (# after a value) are NOT stripped — keep values clean.
/// - Surrounding quotes (" or ') on the value side are stripped once.
/// - Existing environment variables are NOT overwritten (docker/OS always wins).
/// - The file is optional; a missing file is silently ignored.
/// </summary>
internal static class DotEnvLoader
{
    internal static void Load(string filePath)
    {
        if (!TryGetSafeDotEnvPath(filePath, out var safePath))
            return;

        if (!File.Exists(safePath))
            return;

        foreach (var rawLine in File.ReadLines(safePath))
        {
            var line = rawLine.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
                continue;

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            // Strip surrounding single or double quotes.
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"')
                    || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Never overwrite a value already set in the environment (OS / docker wins).
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static bool TryGetSafeDotEnvPath(string filePath, out string safePath)
    {
        safePath = string.Empty;

        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        // Reject common traversal payloads before canonicalization.
        if (filePath.Contains("..", StringComparison.Ordinal))
            return false;

        // DotEnvLoader is intentionally scoped to .env files only.
        if (!string.Equals(Path.GetFileName(filePath), ".env", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            safePath = Path.GetFullPath(filePath);
        }
        catch
        {
            return false;
        }

        return string.Equals(Path.GetFileName(safePath), ".env", StringComparison.OrdinalIgnoreCase);
    }
}


