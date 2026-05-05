using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Configuration;

[TestClass]
public class SecretStartupValidatorTests
{
    [TestMethod]
    public void ValidateOrThrow_Throws_WhenRequiredSecretIsMissing()
    {
        var provider = new DictionarySecretProvider(new Dictionary<string, string?>
        {
            [SecretKeys.DefaultConnectionString] = "Host=localhost;Database=obfuscal;Username=postgres;Password=postgres"
        });

        var validator = new SecretStartupValidator(
            provider,
            Options.Create(new SecretValidationOptions
            {
                RequiredSecretKeys =
                [
                    SecretKeys.DefaultConnectionString,
                    SecretKeys.GraphConsentClientId
                ]
            }));

        var ex = Assert.Throws<InvalidOperationException>(validator.ValidateOrThrow);
        Assert.Contains(SecretKeys.GraphConsentClientId, ex.Message);
    }

    [TestMethod]
    public void ValidateOrThrow_DoesNotThrow_WhenAllRequiredSecretsExist()
    {
        var provider = new DictionarySecretProvider(new Dictionary<string, string?>
        {
            [SecretKeys.DefaultConnectionString] = "Host=localhost;Database=obfuscal;Username=postgres;Password=postgres",
            [SecretKeys.GraphConsentClientId] = "client-id"
        });

        var validator = new SecretStartupValidator(
            provider,
            Options.Create(new SecretValidationOptions
            {
                RequiredSecretKeys =
                [
                    SecretKeys.DefaultConnectionString,
                    SecretKeys.GraphConsentClientId
                ]
            }));

        validator.ValidateOrThrow();
    }

    private sealed class DictionarySecretProvider(IReadOnlyDictionary<string, string?> values) : ISecretProvider
    {
        public string? GetSecret(string key) => values.TryGetValue(key, out var value) ? value : null;
    }
}

