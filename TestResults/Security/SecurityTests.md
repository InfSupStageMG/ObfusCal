# 🔒 ObfusCal Security Test Report

## Executive Summary

| Metric | Value |
|---|---|
| Generated | 2026-06-03 15:12:14 +02:00 |
| Automated Security Tests | 101 |
| Passed | ✅ 101 |
| Failed | ❌ 0 |
| Skipped | ⏭ 0 |
| Automated Feature Rows | 17 |
| Runtime-only Feature Rows | 1 |
| Pass Rate | 100% |

**Source:** `TestResults/Security/SecurityTests.trx`

**Documentation:** See [SECURITY_TESTING.md](../../SECURITY_TESTING.md) for the full validation catalogue and runtime-only checks.

## Security Feature Coverage

| Feature | Verification | Coverage | Status | Notes |
|---|---|---|---|---|
| 1c — Security response headers | Automated | 11/11 | ✅ All pass | Headers are validated at middleware, integration, and production-pipeline levels. |
| 2a — HTTP peer base URL rejected | Automated | 1/1 | ✅ All pass | The admin approve flow rejects unsafe http:// peer endpoints. |
| 2b — HTTPS enforced | Runtime/manual | n/a | ℹ️ Runtime-only | Verify in the container/proxy deployment. TestServer does not reproduce Kestrel TLS redirection semantics. |
| 3a — HTTP iCal URL rejected | Automated | 2/2 | ✅ All pass | Both controller-level and validator-level checks are covered. |
| 3b — Private IP blocked (SSRF) | Automated | 3/3 | ✅ All pass | Controller endpoints and the shared URL validator both block private-network targets. |
| 4a — Cross-owner returns 404 | Automated | 4/4 | ✅ All pass | Both provisioned and auto-provisioned owners get a 404 when probing another owner. |
| 4b — Unauthenticated returns 401 | Automated | 7/7 | ✅ All pass | Anonymous access is rejected consistently for both owner and peer endpoints. |
| 4c/5 — Peer scope isolation | Automated | 3/3 | ✅ All pass | Unmapped owners and missing scopes are both rejected with 403. |
| 6a — Invalid key → 401 | Automated | 1/1 | ✅ All pass | Bad peer credentials never write shadow slots. |
| 6b — Valid key + fresh timestamp | Automated | 2/2 | ✅ All pass | A valid peer request succeeds and also emits an audit event. |
| 6c — Replay attack blocked | Automated | 1/1 | ✅ All pass | Requests outside the allowed timestamp tolerance are rejected. |
| 6d — Revocation takes effect | Automated | 2/2 | ✅ All pass | Revoked peers are rejected immediately in both admin and controller paths. |
| 6e — Key rotation | Automated | 2/2 | ✅ All pass | Old credentials stop working and key rotation is audited. |
| 7a — Missing secret prevents startup | Automated | 9/9 | ✅ All pass | Missing and whitespace-only secrets both fail startup validation. |
| 7b/7c — Credentials redacted | Automated | 21/21 | ✅ All pass | Redaction is verified in both pure unit tests and audit-sink persistence tests. |
| 8a–8d — Rate limiting | Automated | 5/5 | ✅ All pass | Per-peer, per-endpoint, and anonymous backstop quotas are all covered. |
| 9a — PBKDF2 key hashing | Automated | 6/6 | ✅ All pass | Hash format, verification, and storage behaviour are all exercised. |
| 10a–10e — Audit logging | Automated | 6/6 | ✅ All pass | Audit creation, tamper-evidence chaining, and secret redaction are all verified. |

## Detailed Automated Test Results

| Outcome | Test | Duration |
|---|---|---|
| ✅ | `ObfusCal.Tests.Integration.Controllers.AdminPeerConnectionsControllerTests.Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesHttpScheme` | 00:00:00.1699714 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.AdminPeerConnectionsControllerTests.Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesPrivateIpHost` | 00:00:00.1596610 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.AdminPeerConnectionsControllerTests.ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash` | 00:00:00.4162190 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.AdminPeerConnectionsControllerTests.RevokeFlow_SetsRevokedAt_AndPeerAuthStopsImmediately` | 00:00:00.2286070 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.AdminPeerConnectionsControllerTests.RotateFlow_InvalidatesOldApiKeyAndActivatesNewApiKey` | 00:00:00.4091048 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.AdminPeerConnectionsControllerTests.RotateFlow_WritesKeyRotationAuditEvent` | 00:00:00.2461757 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerIcalFeedsTests.AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesHttpScheme` | 00:00:00.1548764 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerIcalFeedsTests.AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesPrivateIpHost` | 00:00:00.1430873 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerIcalFeedsTests.AddIcalFeed_ReturnsUnauthorized_WithoutToken` | 00:00:00.1193677 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerTests.GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId` | 00:00:00.1159608 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerTests.GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId` | 00:00:00.1503111 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerTests.GetBusySlots_ReturnsUnauthorized_WithoutToken` | 00:00:00.1387973 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerTests.GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId` | 00:00:00.1247996 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerTests.GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId` | 00:00:00.1237628 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.CalendarOwnersControllerTests.GetMergedFreeBusy_ReturnsUnauthorized_WithoutToken` | 00:00:00.1153894 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PullBusySlots_WhenPeerExceedsSeparateRateLimit_ReturnsTooManyRequests` | 00:00:00.4083528 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PullBusySlotsForPeer_WithMissingPullScope_ReturnsForbidden` | 00:00:00.3273865 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_DifferentEndpointHasSeparateRateLimit_PullDoesNotBlockPush` | 00:00:00.4959635 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_ReturnsRetryAfterHeaderWithCorrectValue` | 00:00:00.3792463 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WhenPeerExceedsRateLimit_ReturnsTooManyRequestsAndKeepsOtherPeersUnthrottled` | 00:00:00.3689683 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WhenUnauthenticatedRequestExceedsBackstopRateLimit_ReturnsTooManyRequests` | 00:00:00.1465512 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithExpiredReplayTimestamp_ReturnsUnauthorized` | 00:00:00.1564897 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithInvalidApiKey_ReturnsUnauthorizedAndStoresNothing` | 00:00:00.2349354 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithInvalidApiKey_WritesAuthFailureAuditEventWithoutCredentialValue` | 00:00:00.2577027 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithMissingPushScope_ReturnsForbidden` | 00:00:00.3163135 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithoutApiKeyHeader_ReturnsUnauthorized` | 00:00:00.1416977 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithOwnerScopedPayloadForUnmappedOwner_ReturnsForbidden` | 00:00:00.2371710 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithRevokedPeer_ReturnsUnauthorized` | 00:00:00.3325157 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithValidApiKey_StoresSlotsAndReturnsCreated` | 00:00:00.2381586 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithValidApiKey_WritesSuccessAuditEvent` | 00:00:00.4252879 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.ShadowSlotsControllerTests.PushShadowSlots_WithValidApiKeyButNoOwnerMappings_WritesPeerSlotRejectedAuditEvent` | 00:00:00.3451030 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.StatusControllerTests.GetStatus_ReturnsUnauthorized_WhenNotAuthenticated` | 00:00:00.1210451 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.SyncControllerTests.GetPeerSyncStatus_ReturnsUnauthorized_WhenNotAuthenticated` | 00:00:00.1328311 |
| ✅ | `ObfusCal.Tests.Integration.Controllers.SyncControllerTests.TriggerSync_ReturnsUnauthorized_WhenNotAuthenticated` | 00:00:00.0093612 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SecurityHardeningTests.Production_HealthEndpoint_ReturnsExpectedSecurityHeaders` | 00:00:00.1420334 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SecurityHardeningTests.Production_RegistersStrictCookiePolicyDefaults` | 00:00:00.1021104 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_OpenApiJson_ContainsOAuthSecurityDefinition` | 00:00:00.1669567 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_OpenApiJson_IsValidJson` | 00:00:00.2377781 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_SwaggerUi_ContainsConfiguredOAuthClientAndRedirectUri` | 00:00:00.1017311 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_SwaggerUi_IsAvailable` | 00:00:00.1370104 |
| ✅ | `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Production_SwaggerEndpoints_AreNotAccessible` | 00:00:00.1215040 |
| ✅ | `ObfusCal.Tests.Integration.Security.SecurityHeadersIntegrationTests.SecurityHeaders_ArePresent_OnHealthEndpoint` | 00:00:04.6839610 |
| ✅ | `ObfusCal.Tests.Integration.Security.SecurityHeadersIntegrationTests.SecurityHeaders_ArePresent_OnNotFoundResponse` | 00:00:00.1361382 |
| ✅ | `ObfusCal.Tests.Integration.Security.SecurityHeadersIntegrationTests.SecurityHeaders_ArePresent_OnUnauthorizedApiResponse` | 00:00:00.2162886 |
| ✅ | `ObfusCal.Tests.Unit.Configuration.DefaultLogRedactorTests.Redact_MasksBearerTokenAndApiKey` | 00:00:00.0003777 |
| ✅ | `ObfusCal.Tests.Unit.Configuration.DefaultLogRedactorTests.Redact_MasksConnectionStringPassword` | 00:00:00.0002705 |
| ✅ | `ObfusCal.Tests.Unit.Configuration.SecretStartupValidatorTests.ValidateOrThrow_DoesNotThrow_WhenAllRequiredSecretsExist` | 00:00:00.0003910 |
| ✅ | `ObfusCal.Tests.Unit.Configuration.SecretStartupValidatorTests.ValidateOrThrow_Throws_WhenRequiredSecretIsMissing` | 00:00:00.0005624 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Constructor_ThrowsInvalidOperation_WhenKeyIsTooShort` | 00:00:00.0001294 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Constructor_ThrowsInvalidOperation_WhenSecretMissing` | 00:00:00.0002742 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Decrypt_RoundTrips_Correctly` | 00:00:00.0006309 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Decrypt_WithTamperedTag_ThrowsCryptographicException` | 00:00:00.0010109 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Encrypt_EmptyString_RoundTrips` | 00:00:00.0000667 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Encrypt_ProducesCiphertextDifferentFromPlaintext` | 00:00:00.0064926 |
| ✅ | `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Encrypt_SamePlaintext_ProducesDifferentCiphertexts_PerCall` | 00:00:00.0001038 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithAccessToken_RedactsValue` | 00:00:00.0001068 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithApiKeyParameter_RedactsValue` | 00:00:00.0040779 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithBearerToken_RedactsTokenValue` | 00:00:00.0092077 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithClientSecret_RedactsValue` | 00:00:00.0001006 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithCodeParameterInQueryString_RedactsCodeValue` | 00:00:00.0000789 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithConnectionString_RedactsPasswordPart` | 00:00:00.0023467 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithEmptyString_ReturnsEmpty` | 00:00:00.0000574 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithMultipleCredentialTypes_RedactsAll` | 00:00:00.0001405 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithNoSensitiveData_ReturnsUnchanged` | 00:00:00.0000444 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithNullString_ReturnsNull` | 00:00:00.0001146 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithOAuthAuthorizationCode_RedactsCodeValue` | 00:00:00.0001107 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithPassword_RedactsValue` | 00:00:00.0000631 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithRefreshToken_RedactsValue` | 00:00:00.0000751 |
| ✅ | `ObfusCal.Tests.Unit.Security.DefaultLogRedactorTests.Redact_WithWhitespaceString_ReturnsWhitespace` | 00:00:00.0000511 |
| ✅ | `ObfusCal.Tests.Unit.Security.FileSecurityAuditServiceTests.WriteAsync_MultipleEvents_BuildsTamperEvidenceChain` | 00:00:00.1264134 |
| ✅ | `ObfusCal.Tests.Unit.Security.FileSecurityAuditServiceTests.WriteAsync_SanitizesCredentialPatterns_ApiKeyInKeyValue` | 00:00:00.0022706 |
| ✅ | `ObfusCal.Tests.Unit.Security.FileSecurityAuditServiceTests.WriteAsync_SanitizesCredentialPatterns_BearerToken` | 00:00:00.0836924 |
| ✅ | `ObfusCal.Tests.Unit.Security.FileSecurityAuditServiceTests.WriteAsync_SanitizesCredentialPatterns_ConnectionString` | 00:00:00.0497075 |
| ✅ | `ObfusCal.Tests.Unit.Security.FileSecurityAuditServiceTests.WriteAsync_SanitizesMultipleFields` | 00:00:00.0003431 |
| ✅ | `ObfusCal.Tests.Unit.Security.FileSecurityAuditServiceTests.WriteAsync_WithoutRedactor_ExposesSensitiveValuesInAuditLog` | 00:00:00.0473254 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerApiKeySecurityTests.Hash_ProducesPbkdf2FormattedHash_AndVerifyPasses` | 00:00:00.0389153 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerApiKeySecurityTests.LegacySha256Hash_IsDeterministic_RevealingRainbowTableVulnerability` | 00:00:00.0000634 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerApiKeySecurityTests.Pbkdf2Hash_IsNonDeterministic_EvenForSameKey` | 00:00:00.0730694 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerApiKeySecurityTests.Verify_WithDifferentKey_Fails` | 00:00:00.0372634 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerApiKeySecurityTests.Verify_WithLegacySha256Hash_RemainsSupported` | 00:00:00.0002250 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.NormalizeThumbprint_StripsWhitespaceAndSeparators` | 00:00:00.0005961 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.ValidateRemoteCertificate_AcceptsPinnedCertificate_EvenWhenChainErrorsExist` | 00:00:00.0608120 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.ValidateRemoteCertificate_AcceptsSelfSignedCertificate_WhenExplicitlyAllowed` | 00:00:00.0421318 |
| ✅ | `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.ValidateRemoteCertificate_RejectsMismatchedPinnedCertificate` | 00:00:00.0469311 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.ValidateOrThrow_ErrorMessageListsAllMissingKeys` | 00:00:00.0002464 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.ValidateOrThrow_WhenAllRequiredSecretsArePresent_DoesNotThrow` | 00:00:00.0008252 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.ValidateOrThrow_WhenNoRequiredSecretsAreConfigured_DoesNotThrow` | 00:00:00.0000839 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.ValidateOrThrow_WhenRequiredSecretIsMissing_ThrowsInvalidOperationException` | 00:00:00.0003918 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.ValidateOrThrow_WhenRequiredSecretIsNull_ThrowsInvalidOperationException` | 00:00:00.0001778 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.ValidateOrThrow_WhenSecretIsWhitespaceOnly_ThrowsInvalidOperationException` | 00:00:00.0001952 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecretStartupValidatorTests.WithoutValidation_AppWouldStartWithMissingEncryptionKey` | 00:00:00.0001190 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.InvokeAsync_SetsReferrerPolicyHeader` | 00:00:00.0003826 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.InvokeAsync_SetsXContentTypeOptionsHeader` | 00:00:00.0049662 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.InvokeAsync_SetsXFrameOptionsHeader` | 00:00:00.0004350 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.InvokeAsync_StillInvokesNextMiddleware` | 00:00:00.0003329 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.WithoutSecurityHeadersMiddleware_ReferrerPolicyIsAbsent` | 00:00:00.0000751 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.WithoutSecurityHeadersMiddleware_XContentTypeOptionsIsAbsent` | 00:00:00.0001099 |
| ✅ | `ObfusCal.Tests.Unit.Security.SecurityHeadersMiddlewareTests.WithoutSecurityHeadersMiddleware_XFrameOptionsIsAbsent` | 00:00:00.0000749 |
| ✅ | `ObfusCal.Tests.Unit.Security.UrlSafetyValidatorTests.ValidateAsync_ReturnsInvalid_ForHttpScheme` | 00:00:00.0018261 |
| ✅ | `ObfusCal.Tests.Unit.Security.UrlSafetyValidatorTests.ValidateAsync_ReturnsInvalid_ForPrivateIpAddress` | 00:00:00.0005910 |
| ✅ | `ObfusCal.Tests.Unit.Security.UrlSafetyValidatorTests.ValidateAsync_ReturnsValid_ForPublicHttpsUrl` | 00:00:00.0331951 |

## Supporting Security Regression Tests

These tests are part of the curated security suite but are not mapped to a single row in the feature table.

- `ObfusCal.Tests.Integration.Infrastructure.SecurityHardeningTests.Production_RegistersStrictCookiePolicyDefaults`
- `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_OpenApiJson_ContainsOAuthSecurityDefinition`
- `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_OpenApiJson_IsValidJson`
- `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_SwaggerUi_ContainsConfiguredOAuthClientAndRedirectUri`
- `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Development_SwaggerUi_IsAvailable`
- `ObfusCal.Tests.Integration.Infrastructure.SwaggerEndpointsTests.Production_SwaggerEndpoints_AreNotAccessible`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Constructor_ThrowsInvalidOperation_WhenKeyIsTooShort`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Constructor_ThrowsInvalidOperation_WhenSecretMissing`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Decrypt_RoundTrips_Correctly`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Decrypt_WithTamperedTag_ThrowsCryptographicException`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Encrypt_EmptyString_RoundTrips`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Encrypt_ProducesCiphertextDifferentFromPlaintext`
- `ObfusCal.Tests.Unit.Security.AesGcmColumnEncryptorTests.Encrypt_SamePlaintext_ProducesDifferentCiphertexts_PerCall`
- `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.NormalizeThumbprint_StripsWhitespaceAndSeparators`
- `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.ValidateRemoteCertificate_AcceptsPinnedCertificate_EvenWhenChainErrorsExist`
- `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.ValidateRemoteCertificate_AcceptsSelfSignedCertificate_WhenExplicitlyAllowed`
- `ObfusCal.Tests.Unit.Security.PeerTransportSecurityTests.ValidateRemoteCertificate_RejectsMismatchedPinnedCertificate`
- `ObfusCal.Tests.Unit.Security.UrlSafetyValidatorTests.ValidateAsync_ReturnsValid_ForPublicHttpsUrl`

## Reproduce These Results

Generate the same report:

```powershell
.\TestResults\Run-SecurityTestReport.ps1
```

Generate the report without rebuilding first:

```powershell
.\TestResults\Run-SecurityTestReport.ps1 -NoBuild
```

## Presentation Note

- **Automated** rows are backed by executable tests in this report.
- **Runtime/manual** rows require deployment-level evidence such as container inspection, proxy behaviour, or direct environment verification.
- For security review, present this report together with `SECURITY_TESTING.md` so auditors can see both the automated evidence and the residual runtime checks.
