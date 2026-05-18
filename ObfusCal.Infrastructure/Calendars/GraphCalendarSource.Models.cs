using System.Text.Json.Serialization;

namespace ObfusCal.Infrastructure.Calendars;

public sealed partial class GraphCalendarSource
{
    private sealed record GraphCalendarViewResponse(
        [property: JsonPropertyName("value")] List<GraphEvent>? Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphEvent(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("bodyPreview")] string? BodyPreview,
        [property: JsonPropertyName("start")] GraphDateTimeTimeZone? Start,
        [property: JsonPropertyName("end")] GraphDateTimeTimeZone? End,
        [property: JsonPropertyName("attendees")] List<GraphAttendee>? Attendees,
        [property: JsonPropertyName("singleValueExtendedProperties")] List<GraphExtendedProperty>? ExtendedProperties,
        [property: JsonPropertyName("location")] GraphLocation? Location);

    private sealed record GraphDateTimeTimeZone(
        [property: JsonPropertyName("dateTime")] string? DateTime,
        [property: JsonPropertyName("timeZone")] string? TimeZone);

    private sealed record GraphAttendee(
        [property: JsonPropertyName("emailAddress")] GraphEmailAddress? EmailAddress);

    private sealed record GraphEmailAddress(
        [property: JsonPropertyName("address")] string? Address);

    private sealed record GraphLocation(
        [property: JsonPropertyName("displayName")] string? DisplayName);

    private sealed record GraphManagedEventsResponse(
        [property: JsonPropertyName("value")] List<GraphManagedEventDto>? Value,
        [property: JsonPropertyName("@odata.nextLink")] string? NextLink);

    private sealed record GraphManagedEventDto(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("subject")] string? Subject,
        [property: JsonPropertyName("start")] GraphDateTimeTimeZone? Start,
        [property: JsonPropertyName("end")] GraphDateTimeTimeZone? End,
        [property: JsonPropertyName("singleValueExtendedProperties")] List<GraphExtendedProperty>? ExtendedProperties);

    private sealed record GraphExtendedProperty(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("value")] string? Value);

    private sealed record ManagedEventRecord(
        string? GraphId,
        string? SlotId,
        string? Subject,
        DateTimeOffset Start,
        DateTimeOffset End);

    private sealed record GraphTokenRefreshResult(string AccessToken, GraphSourceSecretData SecretData)
    {
        public static GraphTokenRefreshResult Empty(GraphSourceSecretData secretData) => new(string.Empty, secretData);
    }

    private sealed class GraphAccessTokenSession(string accessToken, Func<CancellationToken, Task<string>> refreshAsync)
    {
        public string AccessToken { get; private set; } = accessToken;

        public async Task<bool> TryRefreshAsync(CancellationToken ct)
        {
            var refreshedAccessToken = await refreshAsync(ct);
            if (string.IsNullOrWhiteSpace(refreshedAccessToken))
                return false;

            AccessToken = refreshedAccessToken;
            return true;
        }
    }

    private sealed class GraphInstanceSessionState(GraphSourceSecretData secretData)
    {
        public GraphSourceSecretData SecretData { get; set; } = secretData;
    }

    internal sealed record GraphSourceSecretData(
        string? ProtectedAccessToken,
        string? ProtectedRefreshToken,
        DateTimeOffset? ConsentGrantedAtUtc,
        DateTimeOffset? TokenExpiresAtUtc,
        DateTimeOffset? TokenLastRefreshedAtUtc);
}

