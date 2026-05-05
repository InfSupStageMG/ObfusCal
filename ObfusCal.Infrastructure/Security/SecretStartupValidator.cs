using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

public sealed class SecretStartupValidator(
    ISecretProvider secretProvider,
    IOptions<SecretValidationOptions> validationOptions)
{
    public void ValidateOrThrow()
    {
        var missing = validationOptions.Value.RequiredSecretKeys
            .Where(key => string.IsNullOrWhiteSpace(secretProvider.GetSecret(key)))
            .ToArray();

        if (missing.Length == 0)
            return;

        throw new InvalidOperationException(
            $"Missing required secrets: {string.Join(", ", missing)}. " +
            "Configure the values via environment variables or configuration before startup.");
    }
}

public static class SecretStartupValidationExtensions
{
    public static void ValidateRequiredSecrets(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<SecretStartupValidator>();
        validator.ValidateOrThrow();
    }
}


