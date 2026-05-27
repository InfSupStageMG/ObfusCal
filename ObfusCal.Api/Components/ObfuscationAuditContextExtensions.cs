using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Api.Components;

/// <summary>Display helpers for <see cref="ObfuscationAuditContext"/> in the Blazor UI.</summary>
/// <remarks>
/// The enum values and their database-serialised names (Internal, Client) are intentionally
/// stable and must not be renamed. Only the user-facing labels shown here may change.
/// </remarks>
internal static class ObfuscationAuditContextExtensions
{
    public static string ToDisplayName(this ObfuscationAuditContext context) => context switch
    {
        ObfuscationAuditContext.Internal => "Internal",
        ObfuscationAuditContext.Client   => "External",
        _                                => context.ToString()
    };

    public static string ToDisplayHint(this ObfuscationAuditContext context) => context switch
    {
        ObfuscationAuditContext.Internal =>
            "Controls what you (and your company/coworkers) see on your own availability dashboard — applies when viewing your merged free/busy calendar.",
        ObfuscationAuditContext.Client =>
            "Controls what peers and connected calendars see — applies when your busy slots are pushed to peers or served via the API.",
        _ => string.Empty
    };
}

