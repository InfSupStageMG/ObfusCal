namespace ObfusCal.Application.Configuration;

public static class SecretKeys
{
    public const string DefaultConnectionString = "ConnectionStrings:DefaultConnection";
    public const string GraphConsentClientId = "GraphConsent:ClientId";
    public const string GraphConsentClientSecret = "GraphConsent:ClientSecret";
    public const string GoogleConsentClientId = "GoogleConsent:ClientId";
    public const string GoogleConsentClientSecret = "GoogleConsent:ClientSecret";
    public const string AzureAdClientId = "AzureAd:ClientId";
    public const string AzureAdTenantId = "AzureAd:TenantId";
    public const string SyncInstanceId = "Sync:InstanceId";
    public const string SyncApiKey = "Sync:ApiKey";

    /// <summary>
    /// Base64-encoded 256-bit (32-byte) AES-GCM key used for application-layer column encryption.
    /// Generate with: openssl rand -base64 32
    /// </summary>
    public const string ColumnEncryptionKey = "ColumnEncryption:Key";
}

