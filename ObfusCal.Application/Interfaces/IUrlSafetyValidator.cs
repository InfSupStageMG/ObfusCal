namespace ObfusCal.Application.Interfaces;

public interface IUrlSafetyValidator
{
    Task<UrlSafetyValidationResult> ValidateAsync(string url, CancellationToken ct = default);
    Task<UrlSafetyValidationResult> ValidateAsync(Uri uri, CancellationToken ct = default);
}

public enum UrlSafetyValidationError
{
    None,
    MissingOrInvalidAbsoluteUrl,
    UnsupportedScheme,
    PrivateNetworkHost
}

public sealed record UrlSafetyValidationResult(
    bool IsValid,
    UrlSafetyValidationError Error,
    string Message)
{
    public static UrlSafetyValidationResult Success() =>
        new(true, UrlSafetyValidationError.None, string.Empty);

    public static UrlSafetyValidationResult Fail(UrlSafetyValidationError error, string message) =>
        new(false, error, message);
}

