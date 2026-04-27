namespace ObfusCal.Application.Configuration;

public sealed class GraphConsentOptions
{
    public const string SectionName = "GraphConsent";

    public string Scope { get; init; } = "https://graph.microsoft.com/Calendars.Read offline_access";
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
}

