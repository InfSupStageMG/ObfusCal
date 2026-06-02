using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Infrastructure.Security;

/// <summary>
/// Validates user-provided URLs to prevent Server-Side Request Forgery (SSRF)
/// by blocking private, loopback, and link-local IP ranges.
/// </summary>
internal sealed class UrlSafetyValidator : IUrlSafetyValidator
{
    private readonly bool _allowPrivateNetworkHosts;

    // Parameterless constructor used by tests and as a fallback in IcalFeedCalendarSource.
    // Defaults to strict/production behaviour (private hosts rejected).
    public UrlSafetyValidator() { }

    public UrlSafetyValidator(IOptions<PeerTransportSecurityOptions> options)
    {
        _allowPrivateNetworkHosts = options.Value.AllowPrivateNetworkHosts;
    }
    public Task<UrlSafetyValidationResult> ValidateAsync(string url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return Task.FromResult(UrlSafetyValidationResult.Fail(
                UrlSafetyValidationError.MissingOrInvalidAbsoluteUrl,
                "A valid absolute URL is required."));
        }

        return ValidateAsync(uri, ct);
    }

    public async Task<UrlSafetyValidationResult> ValidateAsync(Uri uri, CancellationToken ct = default)
    {
        if (!uri.IsAbsoluteUri)
        {
            return UrlSafetyValidationResult.Fail(
                UrlSafetyValidationError.MissingOrInvalidAbsoluteUrl,
                "A valid absolute URL is required.");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return UrlSafetyValidationResult.Fail(
                UrlSafetyValidationError.UnsupportedScheme,
                "Only https URLs are allowed.");
        }

        if (!_allowPrivateNetworkHosts && TryRejectPrivateHost(uri.Host, out var privateHostResult))
            return privateHostResult!;

        IPAddress[] resolvedAddresses;
        try
        {
            resolvedAddresses = await Dns.GetHostAddressesAsync(uri.DnsSafeHost, ct);
        }
        catch (SocketException)
        {
            // If DNS cannot currently resolve, keep validation permissive and re-check at request time.
            return UrlSafetyValidationResult.Success();
        }

        if (!_allowPrivateNetworkHosts && resolvedAddresses.Any(IsPrivateOrLocalAddress))
        {
            return UrlSafetyValidationResult.Fail(
                UrlSafetyValidationError.PrivateNetworkHost,
                "Hosts that resolve to private, loopback, or link-local IP addresses are not allowed.");
        }

        return UrlSafetyValidationResult.Success();
    }

    private static bool TryRejectPrivateHost(string host, out UrlSafetyValidationResult? result)
    {
        result = null;

        if (!IPAddress.TryParse(host, out var ipAddress))
            return false;

        if (!IsPrivateOrLocalAddress(ipAddress))
            return false;

        result = UrlSafetyValidationResult.Fail(
            UrlSafetyValidationError.PrivateNetworkHost,
            "Private, loopback, and link-local IP addresses are not allowed.");
        return true;
    }

    private static bool IsPrivateOrLocalAddress(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                10 => true,
                172 when bytes[1] is >= 16 and <= 31 => true,
                192 when bytes[1] == 168 => true,
                169 when bytes[1] == 254 => true,
                _ => false
            };
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return false;

        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            return true;

        var bytesV6 = address.GetAddressBytes();
        return (bytesV6[0] & 0xFE) == 0xFC; // fc00::/7 unique local address range
    }
}

