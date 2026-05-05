namespace ObfusCal.Application.Configuration;

public sealed class SecretValidationOptions
{
    public List<string> RequiredSecretKeys { get; init; } = [];
}

