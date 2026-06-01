using System.Reflection;

namespace ObfusCal.Api.Components.Layout;

/// <summary>
/// Provides the current application version for the Blazor UI.
/// </summary>
public sealed class ApplicationVersionProvider
{
    public ApplicationVersionProvider()
        : this(Assembly.GetExecutingAssembly())
    {
    }

    internal ApplicationVersionProvider(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        DisplayVersion = ResolveDisplayVersion(assembly);
    }

    public string DisplayVersion { get; }

    public static string NormalizeDisplayVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
            return "dev";

        var trimmed = version.Trim();
        var metadataIndex = trimmed.IndexOf('+');
        if (metadataIndex >= 0)
            trimmed = trimmed[..metadataIndex];

        if (trimmed.Length > 1 && trimmed[0] == 'v' && char.IsDigit(trimmed[1]))
            trimmed = trimmed[1..];

        return string.IsNullOrWhiteSpace(trimmed) ? "dev" : trimmed;
    }

    private static string ResolveDisplayVersion(Assembly assembly)
    {
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return NormalizeDisplayVersion(!string.IsNullOrWhiteSpace(informationalVersion) ? informationalVersion : assembly.GetName().Version?.ToString());
    }
}

