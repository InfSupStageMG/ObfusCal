namespace ObfusCal.Application.Interfaces;

public interface IPeerApiKeyAuthenticator
{
    Task<PeerApiKeyAuthenticationResult?> AuthenticateAsync(string providedApiKey, CancellationToken ct = default);
}

public sealed record PeerApiKeyAuthenticationResult(
    Guid PeerConnectionId,
    string PeerInstanceId,
    IReadOnlyList<string> Scopes);

