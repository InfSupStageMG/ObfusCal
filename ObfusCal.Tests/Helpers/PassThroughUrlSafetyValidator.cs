using ObfusCal.Application.Interfaces;

namespace ObfusCal.Tests.Helpers;

/// <summary>
/// A test stub that approves every URL unconditionally, simulating the absence of SSRF protection.
/// Use via <see cref="CustomWebApplicationFactory"/> with <c>disableUrlSafetyValidation: true</c>.
/// </summary>
internal sealed class PassThroughUrlSafetyValidator : IUrlSafetyValidator
{
    public Task<UrlSafetyValidationResult> ValidateAsync(string url, CancellationToken ct = default) =>
        Task.FromResult(UrlSafetyValidationResult.Success());

    public Task<UrlSafetyValidationResult> ValidateAsync(Uri uri, CancellationToken ct = default) =>
        Task.FromResult(UrlSafetyValidationResult.Success());
}

