using System.Text.RegularExpressions;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class DefaultLogRedactor : ILogRedactor
{
    private static readonly (Regex Pattern, string Replacement)[] Rules =
    [
        (new Regex("(Bearer\\s+)[A-Za-z0-9\\-._~+/]+=*", RegexOptions.Compiled | RegexOptions.IgnoreCase), "$1[REDACTED]"),
        (new Regex("(?<key>(api[-_]?key|client[-_]?secret|password|access[-_]?token|refresh[-_]?token|code)\\s*[=:]\\s*)(?<value>[^\\s,;]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase), "${key}[REDACTED]"),
        (new Regex("(Host=.*?;Database=.*?;Username=.*?;Password=)([^;\\s]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase), "$1[REDACTED]")
    ];

    public string Redact(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        var redacted = input;
        foreach (var (pattern, replacement) in Rules)
            redacted = pattern.Replace(redacted, replacement);

        return redacted;
    }
}

