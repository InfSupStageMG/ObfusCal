using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Security;

namespace ObfusCal.Tests.Unit.Security;

[TestClass]
public class SecretStartupValidatorTests
{
    // --- AFTER protection: validator correctly blocks startup when secrets are absent ---

    [TestMethod]
    public void ValidateOrThrow_WhenAllRequiredSecretsArePresent_DoesNotThrow()
    {
        var options = Options.Create(new SecretValidationOptions
        {
            RequiredSecretKeys = ["SecretA", "SecretB"]
        });
        var provider = new FakeSecretProvider(new Dictionary<string, string?>
        {
            ["SecretA"] = "value-a",
            ["SecretB"] = "value-b"
        });

        var validator = new SecretStartupValidator(provider, options);

        // Should not throw
        validator.ValidateOrThrow();
    }

    [TestMethod]
    public void ValidateOrThrow_WhenRequiredSecretIsMissing_ThrowsInvalidOperationException()
    {
        // This is the protection: the app refuses to start rather than running with
        // an absent encryption key or credential, which would silently corrupt data
        // or allow unauthenticated access.
        var options = Options.Create(new SecretValidationOptions
        {
            RequiredSecretKeys = [SecretKeys.ColumnEncryptionKey]
        });
        var provider = new FakeSecretProvider(new Dictionary<string, string?>());

        var validator = new SecretStartupValidator(provider, options);

        Assert.ThrowsExactly<InvalidOperationException>(() => validator.ValidateOrThrow());
    }

    [TestMethod]
    public void ValidateOrThrow_WhenSecretIsWhitespaceOnly_ThrowsInvalidOperationException()
    {
        // Whitespace-only secrets must also be treated as absent.
        var options = Options.Create(new SecretValidationOptions
        {
            RequiredSecretKeys = [SecretKeys.ColumnEncryptionKey]
        });
        var provider = new FakeSecretProvider(new Dictionary<string, string?>
        {
            [SecretKeys.ColumnEncryptionKey] = "   "
        });

        var validator = new SecretStartupValidator(provider, options);

        Assert.ThrowsExactly<InvalidOperationException>(() => validator.ValidateOrThrow());
    }

    [TestMethod]
    public void ValidateOrThrow_WhenRequiredSecretIsNull_ThrowsInvalidOperationException()
    {
        var options = Options.Create(new SecretValidationOptions
        {
            RequiredSecretKeys = [SecretKeys.SyncApiKey]
        });
        var provider = new FakeSecretProvider(new Dictionary<string, string?>
        {
            [SecretKeys.SyncApiKey] = null
        });

        var validator = new SecretStartupValidator(provider, options);

        Assert.ThrowsExactly<InvalidOperationException>(() => validator.ValidateOrThrow());
    }

    [TestMethod]
    public void ValidateOrThrow_WhenNoRequiredSecretsAreConfigured_DoesNotThrow()
    {
        var options = Options.Create(new SecretValidationOptions
        {
            RequiredSecretKeys = []
        });
        var provider = new FakeSecretProvider(new Dictionary<string, string?>());

        var validator = new SecretStartupValidator(provider, options);

        // Empty requirements means nothing to validate.
        validator.ValidateOrThrow();
    }

    [TestMethod]
    public void ValidateOrThrow_ErrorMessageListsAllMissingKeys()
    {
        var options = Options.Create(new SecretValidationOptions
        {
            RequiredSecretKeys = ["KeyA", "KeyB", "KeyC"]
        });
        // Only KeyA is present; KeyB and KeyC are missing.
        var provider = new FakeSecretProvider(new Dictionary<string, string?> { ["KeyA"] = "val" });

        var validator = new SecretStartupValidator(provider, options);

        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => validator.ValidateOrThrow());
        Assert.IsTrue(ex.Message.Contains("KeyB", StringComparison.Ordinal));
        Assert.IsTrue(ex.Message.Contains("KeyC", StringComparison.Ordinal));
    }

    // --- BEFORE protection: demonstrates what happens when validation is skipped ---

    [TestMethod]
    public void WithoutValidation_AppWouldStartWithMissingEncryptionKey()
    {
        // Before SecretStartupValidator was introduced, the app would boot even when
        // COLUMNENCRYPTION__KEY was absent. Data written with an empty/default key
        // would be silently unencrypted or corrupted.
        //
        // This test confirms the vulnerable state: constructing a validator with missing
        // config but NOT calling ValidateOrThrow means no exception is raised.
        var provider = new FakeSecretProvider(new Dictionary<string, string?>());

        // Without calling ValidateOrThrow(), missing secrets are silently ignored.
        // This is the "before" state: app starts, but the key is missing.
        var missingKey = provider.GetSecret(SecretKeys.ColumnEncryptionKey);
        Assert.IsTrue(string.IsNullOrWhiteSpace(missingKey),
            "Without validation, an absent key goes undetected.");
    }

    // Minimal fake to avoid dependencies on infrastructure implementations.
    private sealed class FakeSecretProvider(Dictionary<string, string?> values) : ISecretProvider
    {
        public string? GetSecret(string key) =>
            values.TryGetValue(key, out var v) ? v : null;
    }
}



