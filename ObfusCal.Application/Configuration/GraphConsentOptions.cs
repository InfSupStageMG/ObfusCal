namespace ObfusCal.Application.Configuration;

public sealed class GraphConsentOptions
{
    public const string SectionName = "GraphConsent";

    public string ApiBaseUrl { get; init; } = "https://graph.microsoft.com";
    public string Scope { get; init; } = "https://graph.microsoft.com/Calendars.ReadWrite offline_access";
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}

