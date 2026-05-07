using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ObfusCal.Application.Configuration;
using ObfusCal.Application.Interfaces;
using ObfusCal.Infrastructure.Persistence;
using ObfusCal.Tests.Helpers;

namespace ObfusCal.Tests.Unit.Persistence;

[TestClass]
public class CalendarOwnerGoogleConsentServiceTests
{
    [TestMethod]
    public async Task BuildAuthorizationUrlAsync_IncludesSelectAccountPrompt()
    {
        var (service, ownerId, _) = Setup();

        var authorizationUrl = await service.BuildAuthorizationUrlAsync(ownerId, "https://localhost/consent-callback");

        Assert.Contains(Uri.EscapeDataString("select_account consent"), authorizationUrl);
    }

    [TestMethod]
    public async Task BuildAuthorizationUrlAsync_UsesConfiguredRedirectUri_WhenPresent()
    {
        var configuredRedirectUri = "https://localhost/consent-callback";
        var (service, ownerId, _) = Setup(new GoogleConsentOptions
        {
            AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
            TokenEndpoint = "https://oauth2.googleapis.com/token",
            Scope = "https://www.googleapis.com/auth/calendar.readonly",
            ClientId = "google-client-id",
            RedirectUri = configuredRedirectUri
        });

        var authorizationUrl = await service.BuildAuthorizationUrlAsync(ownerId, "https://obfuscal.local/consent-callback");

        Assert.Contains(Uri.EscapeDataString(configuredRedirectUri), authorizationUrl);
        Assert.DoesNotContain(Uri.EscapeDataString("https://obfuscal.local/consent-callback"), authorizationUrl);
    }

    [TestMethod]
    public async Task BuildAuthorizationUrlAsync_WithLocalDomainRedirectUri_ThrowsClearException()
    {
        var (service, ownerId, _) = Setup();

        var exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            service.BuildAuthorizationUrlAsync(ownerId, "https://obfuscal.local/consent-callback"));

        Assert.Contains(".local", exception.Message);
        Assert.Contains("GoogleConsent:RedirectUri", exception.Message);
    }

    [TestMethod]
    public async Task CompleteConsentFromStateAsync_UsesRedirectUriStoredInState()
    {
        var configuredRedirectUri = "https://localhost/consent-callback";
        var tokenClient = new CapturingGoogleOAuthTokenClient();
        var (service, ownerId, _) = Setup(
            new GoogleConsentOptions
            {
                AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                TokenEndpoint = "https://oauth2.googleapis.com/token",
                Scope = "https://www.googleapis.com/auth/calendar.readonly",
                ClientId = "google-client-id",
                RedirectUri = configuredRedirectUri
            },
            tokenClient);

        var authorizationUrl = await service.BuildAuthorizationUrlAsync(ownerId, "https://obfuscal.local/consent-callback");
        var state = GetQueryValue(authorizationUrl, "state");

        await service.CompleteConsentFromStateAsync("authorization-code", state);

        Assert.AreEqual(configuredRedirectUri, tokenClient.LastRedirectUri);

        var status = await service.GetStatusAsync(ownerId);
        Assert.IsNotNull(status);
        Assert.IsTrue(status.HasGoogleConsent);
    }

    private static (CalendarOwnerGoogleConsentService Service, Guid OwnerId, CapturingGoogleOAuthTokenClient TokenClient) Setup(
        GoogleConsentOptions? googleConsentOptions = null,
        CapturingGoogleOAuthTokenClient? tokenClient = null)
    {
        var dbContext = TestDbContextFactory.CreateInMemory();
        var ownerId = Guid.NewGuid();
        dbContext.CalendarOwners.Add(new CalendarOwner { Id = ownerId, Name = "Test" });
        dbContext.SaveChanges();

        tokenClient ??= new CapturingGoogleOAuthTokenClient();
        var instances = new FakeCalendarSourceInstanceService(calendarOwnerId => dbContext.CalendarOwners.Any(owner => owner.Id == calendarOwnerId));
        var logger = LoggerFactory.Create(_ => { }).CreateLogger<CalendarOwnerGoogleConsentService>();
        var service = new CalendarOwnerGoogleConsentService(
            dbContext,
            DataProtectionProvider.Create("google-consent-tests"),
            new DictionarySecretProvider(new Dictionary<string, string?>
            {
                [SecretKeys.GoogleConsentClientId] = "google-client-id"
            }),
            new GoogleOAuthDependencies(
                Options.Create(googleConsentOptions ?? new GoogleConsentOptions
                {
                    AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth",
                    TokenEndpoint = "https://oauth2.googleapis.com/token",
                    Scope = "https://www.googleapis.com/auth/calendar.readonly",
                    ClientId = "google-client-id"
                }),
                tokenClient),
            new PassthroughSecretProtector(),
            new GoogleConsentInstanceDependencies(instances, instances),
            logger);

        return (service, ownerId, tokenClient);
    }

    private static string GetQueryValue(string url, string key)
    {
        var query = new Uri(url).Query.TrimStart('?');
        foreach (var segment in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = segment.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], key, StringComparison.Ordinal))
                return Uri.UnescapeDataString(parts[1]);
        }

        Assert.Fail($"Query parameter '{key}' was not found.");
        return string.Empty;
    }

    private sealed class CapturingGoogleOAuthTokenClient : IGoogleOAuthTokenClient
    {
        public string? LastRedirectUri { get; private set; }

        public Task<GoogleOAuthTokenResponse> ExchangeAuthorizationCodeAsync(
            string authorizationCode,
            string redirectUri,
            CancellationToken ct = default)
        {
            LastRedirectUri = redirectUri;
            return Task.FromResult(new GoogleOAuthTokenResponse(
                "access-token",
                "refresh-token",
                DateTimeOffset.UtcNow.AddHours(1)));
        }

        public Task<GoogleOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken ct = default)
        {
            return Task.FromResult(new GoogleOAuthTokenResponse(
                "new-access-token",
                refreshToken,
                DateTimeOffset.UtcNow.AddHours(1)));
        }
    }

    private sealed class PassthroughSecretProtector : ICalendarSourceSecretProtector
    {
        public string Protect(string plaintext) => plaintext;

        public string Unprotect(string protectedValue) => protectedValue;
    }

    private sealed class DictionarySecretProvider(IReadOnlyDictionary<string, string?> values) : ISecretProvider
    {
        public string? GetSecret(string key) => values.TryGetValue(key, out var value) ? value : null;
    }
}

