namespace ObfusCal.Application.Configuration;

public sealed class SecretProviderOptions
{
    public const string SectionName = "Secrets";

    // Supported values: Environment, External.
    public string Provider { get; init; } = "Environment";
}

