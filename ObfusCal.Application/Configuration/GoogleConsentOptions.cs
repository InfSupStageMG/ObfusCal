namespace ObfusCal.Application.Configuration;

public sealed class GoogleConsentOptions
{
    public const string SectionName = "GoogleConsent";

    public string ApiBaseUrl { get; init; } = string.Empty;
    public string AuthorizationEndpoint { get; init; } = string.Empty;
    public string TokenEndpoint { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}

