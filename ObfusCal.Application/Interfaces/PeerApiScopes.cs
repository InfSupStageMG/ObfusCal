namespace ObfusCal.Application.Interfaces;

public static class PeerApiScopes
{
    public const string PushShadowSlots = "push_shadow_slots";
    public const string PullBusySlots = "pull_busy_slots";

    public static readonly string[] DefaultScopes = [PushShadowSlots, PullBusySlots];
    public static readonly string DefaultSerializedScopes = string.Join(' ', DefaultScopes);

    public static string Normalize(IEnumerable<string> scopes)
    {
        var normalized = scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0 ? DefaultSerializedScopes : string.Join(' ', normalized);
    }
}

