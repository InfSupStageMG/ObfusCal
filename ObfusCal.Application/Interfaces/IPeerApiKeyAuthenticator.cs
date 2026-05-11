namespace ObfusCal.Application.Interfaces;

/// <summary>
/// Validates peer API keys and returns the peer identity and scopes for authenticated requests.
/// </summary>
public interface IPeerApiKeyAuthenticator
{
    Task<PeerApiKeyAuthenticationResult?> AuthenticateAsync(string providedApiKey, CancellationToken ct = default);
}

public sealed record PeerApiKeyAuthenticationResult(
    Guid PeerConnectionId,
    string PeerInstanceId,
    IReadOnlyList<string> Scopes);

