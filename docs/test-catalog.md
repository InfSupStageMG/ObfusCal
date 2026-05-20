# Test Catalog

_Auto-generated from CI run. 609 tests across 64 classes._

## Integration (175/175)

<details>
<summary><b>AdminPeerConnectionsControllerTests</b> - 7/7</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>AdminEndpoints_EnforceRoleAndAuthentication</code></td><td>✅</td></tr>
    <tr><td><code>ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash</code></td><td>✅</td></tr>
    <tr><td><code>Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesHttpScheme</code></td><td>✅</td></tr>
    <tr><td><code>Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesPrivateIpHost</code></td><td>✅</td></tr>
    <tr><td><code>RevokeFlow_SetsRevokedAt_AndPeerAuthStopsImmediately</code></td><td>✅</td></tr>
    <tr><td><code>RotateFlow_InvalidatesOldApiKeyAndActivatesNewApiKey</code></td><td>✅</td></tr>
    <tr><td><code>SuspendFlow_SetsStatusToSuspended_AndAuthStopsImmediately</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnersControllerIcalFeedsTests</b> - 12/12</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>AddIcalFeed_DoesNotChangePlugin_WhenOwnerAlreadyUsesNonMockProvider</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsBadRequest_WhenFeedUrlIsInvalid</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesHttpScheme</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesPrivateIpHost</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsConflict_WhenFeedAlreadyExists</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsCreated_AndPersistsFeed</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsUnauthorized_WithoutToken</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_ReturnsValidationProblemDetails_WhenFeedUrlIsMissing</code></td><td>✅</td></tr>
    <tr><td><code>AddIcalFeed_SwitchesCalendarSourcePluginToIcal_WhenOwnerUsesPlaceholderProvider</code></td><td>✅</td></tr>
    <tr><td><code>DeleteIcalFeed_ReturnsNoContent_AndRemovesFeed</code></td><td>✅</td></tr>
    <tr><td><code>DeleteIcalFeed_ReturnsNotFound_WhenFeedDoesNotExist</code></td><td>✅</td></tr>
    <tr><td><code>ListIcalFeeds_ReturnsOk_WithConfiguredFeeds</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnersControllerTests</b> - 37/37</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>CompleteCalendarConsent_ReturnsBadRequest_WhenAuthorizationCodeIsInvalid</code></td><td>✅</td></tr>
    <tr><td><code>CompleteCalendarConsent_ReturnsBadRequest_WhenAuthorizationCodeMissing</code></td><td>✅</td></tr>
    <tr><td><code>CompleteCalendarConsent_ReturnsBadRequest_WhenRedirectUriInvalid</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_BadRequest_ContainsMeaningfulMessage</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsBadRequest_WhenFromIsInvalid</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsBadRequest_WhenFromIsMissing</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsBadRequest_WhenToIsInvalid</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsBadRequest_WhenToIsMissing</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsBadRequest_WhenWindowExceedsConfiguredMaximum</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsConflict_WhenCalendarOwnerHasNotGrantedGraphConsent</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsOk_WithValidParameters</code></td><td>✅</td></tr>
    <tr><td><code>GetBusySlots_ReturnsUnauthorized_WithoutToken</code></td><td>✅</td></tr>
    <tr><td><code>GetCalendarConsentStatus_ReturnsFalseBeforeConsent_AndTrueAfterCompletion</code></td><td>✅</td></tr>
    <tr><td><code>GetCalendarConsentUrl_ReturnsAuthorizationUrl_ForAuthenticatedCalendarOwner</code></td><td>✅</td></tr>
    <tr><td><code>GetCalendarConsentUrl_ReturnsBadRequest_WhenRedirectUriIsRelative</code></td><td>✅</td></tr>
    <tr><td><code>GetCalendarConsentUrl_ReturnsBadRequest_WhenRedirectUriMissing</code></td><td>✅</td></tr>
    <tr><td><code>GetCurrentCalendarOwner_ReturnsAuthenticatedObjectId</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_BadRequest_ContainsMeaningfulMessage</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsInvalid</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsMissing</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsBadRequest_WhenToIsInvalid</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsBadRequest_WhenToIsMissing</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsBadRequest_WhenWindowExceedsConfiguredMaximum</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsJsonWithStartAndEndFields</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsOk_WithValidParameters</code></td><td>✅</td></tr>
    <tr><td><code>GetMergedFreeBusy_ReturnsUnauthorized_WithoutToken</code></td><td>✅</td></tr>
    <tr><td><code>ListObfuscationProfiles_ReturnsDefaultProfiles_ForAuthenticatedCalendarOwner</code></td><td>✅</td></tr>
    <tr><td><code>ListObfuscationProfiles_ReturnsNotFound_ForDifferentOwner</code></td><td>✅</td></tr>
    <tr><td><code>SetObfuscationProfile_ResponseContainsAllFields</code></td><td>✅</td></tr>
    <tr><td><code>SetObfuscationProfile_ReturnsBadRequest_ForUnknownContext</code></td><td>✅</td></tr>
    <tr><td><code>SetObfuscationProfile_ReturnsBadRequest_ForZeroRoundingInterval</code></td><td>✅</td></tr>
    <tr><td><code>SetObfuscationProfile_ReturnsNotFound_ForDifferentOwner</code></td><td>✅</td></tr>
    <tr><td><code>SetObfuscationProfile_UpdatesClientProfile_ForAuthenticatedCalendarOwner</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>EntraIdSysadminRoleTests</b> - 6/6</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>AdminEndpoints_AcceptSysadminRoleClaim_WithoutCalendarOwnerRecord</code></td><td>✅</td></tr>
    <tr><td><code>HasSysadminRole_ReturnsFalse_ForDifferentRole</code></td><td>✅</td></tr>
    <tr><td><code>HasSysadminRole_ReturnsTrue_ForClaimTypesRole</code></td><td>✅</td></tr>
    <tr><td><code>HasSysadminRole_ReturnsTrue_ForEntraRolesClaim</code></td><td>✅</td></tr>
    <tr><td><code>NonSysadminUser_CannotAccessAdminEndpoints</code></td><td>✅</td></tr>
    <tr><td><code>UnknownUser_CannotAccessAdminEndpoints</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PeerConnectionsControllerTests</b> - 6/6</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ListPeerConnections_ReturnsOnlyCurrentConsultantRequests</code></td><td>✅</td></tr>
    <tr><td><code>RequestPeerConnection_AutoProvisionsCalendarOwner_WhenMissing</code></td><td>✅</td></tr>
    <tr><td><code>RequestPeerConnection_CreatesRequestedRecord_ForAuthenticatedConsultant</code></td><td>✅</td></tr>
    <tr><td><code>RequestPeerConnection_ReturnsConflict_WhenDuplicateForSameConsultant</code></td><td>✅</td></tr>
    <tr><td><code>RequestPeerConnection_ReturnsUnauthorized_WhenUnauthenticated</code></td><td>✅</td></tr>
    <tr><td><code>RequestPeerConnection_ReturnsValidationProblemDetails_WhenClientOrganisationNameMissing</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ShadowSlotsControllerTests</b> - 17/17</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>PullBusySlotsForPeer_WithMissingPullScope_ReturnsForbidden</code></td><td>✅</td></tr>
    <tr><td><code>PullBusySlotsForPeer_WithValidApiKeyAndMapping_ReturnsOk</code></td><td>✅</td></tr>
    <tr><td><code>PullBusySlotsForPeer_WithValidApiKeyButNoMapping_ReturnsForbidden</code></td><td>✅</td></tr>
    <tr><td><code>PullBusySlotsForPeer_WithoutApiKey_ReturnsUnauthorized</code></td><td>✅</td></tr>
    <tr><td><code>PullBusySlots_WhenPeerExceedsSeparateRateLimit_ReturnsTooManyRequests</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_ReturnsBadRequest_WhenSlotBatchExceedsMaximum</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_ReturnsPayloadTooLarge_WhenRequestBodyExceedsConfiguredLimit</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WhenPeerExceedsRateLimit_ReturnsTooManyRequestsAndKeepsOtherPeersUnthrottled</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithExpiredReplayTimestamp_ReturnsUnauthorized</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithInvalidApiKey_ReturnsUnauthorizedAndStoresNothing</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithMissingPushScope_ReturnsForbidden</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithOwnerScopedPayloadForUnmappedOwner_ReturnsForbidden</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithOwnerScopedPayload_StoresSlotsAndReturnsCreated</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithRevokedPeer_ReturnsUnauthorized</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithValidApiKeyButNoOwnerMappings_ReturnsForbidden</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithValidApiKey_StoresSlotsAndReturnsCreated</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_WithoutApiKeyHeader_ReturnsUnauthorized</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>StatusControllerTests</b> - 8/8</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetStatus_IncludesCalendarOwnerFields</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_IncludesPeerConnectionStatus</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_IncludesPeerLastSyncMetadata</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_NullTimestamps_WhenNeverSynced</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_ReturnsEmptyArray_WhenNoCalendarOwners</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_ReturnsForbidden_WhenCallerLacksSysadminRole</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_ReturnsOk_WhenCallerHasSysadminRole</code></td><td>✅</td></tr>
    <tr><td><code>GetStatus_ReturnsUnauthorized_WhenNotAuthenticated</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>SyncControllerTests</b> - 11/11</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetPeerSyncStatus_ReturnsForbidden_WhenCallerLacksSysadminRole</code></td><td>✅</td></tr>
    <tr><td><code>GetPeerSyncStatus_ReturnsOk_WhenCallerHasSysadminRole</code></td><td>✅</td></tr>
    <tr><td><code>GetPeerSyncStatus_ReturnsUnauthorized_WhenNotAuthenticated</code></td><td>✅</td></tr>
    <tr><td><code>PullBusySlotsForPeer_ReturnsBadRequest_WhenFromMissing</code></td><td>✅</td></tr>
    <tr><td><code>PullBusySlotsForPeer_ReturnsBadRequest_WhenToMissing</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_CreatedResponse_ContainsLocationHeader</code></td><td>✅</td></tr>
    <tr><td><code>PushShadowSlots_ReturnsBadRequest_WithInvalidPayload</code></td><td>✅</td></tr>
    <tr><td><code>TriggerSync_ReturnsAccepted_WhenCallerHasSysadminRole</code></td><td>✅</td></tr>
    <tr><td><code>TriggerSync_ReturnsAccepted_WhenTargetingSpecificOwner</code></td><td>✅</td></tr>
    <tr><td><code>TriggerSync_ReturnsForbidden_WhenCallerLacksSysadminRole</code></td><td>✅</td></tr>
    <tr><td><code>TriggerSync_ReturnsUnauthorized_WhenNotAuthenticated</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>BrowserSsoTests</b> - 10/10</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>CalendarOwnerDetail_DeniesAccessToDifferentOwner_ForNonSysadminUser</code></td><td>✅</td></tr>
    <tr><td><code>CalendarOwnersPage_Loads_ForSysadminUser</code></td><td>✅</td></tr>
    <tr><td><code>CalendarOwnersPage_ShowsUnauthorizedMessage_ForNonSysadminUser</code></td><td>✅</td></tr>
    <tr><td><code>Dashboard_AutoProvisionsCalendarOwner_ForSignedInUser_WhenMissing</code></td><td>✅</td></tr>
    <tr><td><code>Dashboard_IsScopedToSignedInUsersCalendarOwner_WhenNotSysadmin</code></td><td>✅</td></tr>
    <tr><td><code>Dashboard_RendersLoginRedirect_ForAnonymousUser</code></td><td>✅</td></tr>
    <tr><td><code>LoginEndpoint_RedirectsToEntraAuthorizeEndpoint</code></td><td>✅</td></tr>
    <tr><td><code>PeerConnectionsPage_LoadsAdminView_ForSysadminUser</code></td><td>✅</td></tr>
    <tr><td><code>PeerConnectionsPage_LoadsReadOnlyView_ForNonSysadminUser</code></td><td>✅</td></tr>
    <tr><td><code>SwitchEndpoint_RedirectsToEntraAuthorizeEndpoint_WithSelectAccountPrompt</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>InfrastructureDependencyInjectionTests</b> - 12/12</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>CalendarOwnerCalendarSourceService_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarOwnerGraphConsentService_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarOwnerIcalFeedService_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarOwnerObfuscationProfileService_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarOwnerScopeResolver_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarSourceCatalog_ContainsAllThreeBuiltInPlugins</code></td><td>✅</td></tr>
    <tr><td><code>CalendarSourceCatalog_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarSourceResolver_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>CalendarSource_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>InboundPeerPullSyncService_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>OutboundPeerSyncService_IsRegistered</code></td><td>✅</td></tr>
    <tr><td><code>ShadowSlotStore_IsRegistered</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>SecurityHardeningTests</b> - 2/2</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Production_HealthEndpoint_ReturnsExpectedSecurityHeaders</code></td><td>✅</td></tr>
    <tr><td><code>Production_RegistersStrictCookiePolicyDefaults</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ShadowSlotStoreDependencyInjectionTests</b> - 1/1</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ShadowSlotStore_IsRegisteredAsSingleton</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>SwaggerEndpointsTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Development_OpenApiJson_ContainsOAuthSecurityDefinition</code></td><td>✅</td></tr>
    <tr><td><code>Development_OpenApiJson_IsValidJson</code></td><td>✅</td></tr>
    <tr><td><code>Development_SwaggerUi_ContainsConfiguredOAuthClientAndRedirectUri</code></td><td>✅</td></tr>
    <tr><td><code>Development_SwaggerUi_IsAvailable</code></td><td>✅</td></tr>
    <tr><td><code>Production_SwaggerEndpoints_AreNotAccessible</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>EfCoreShadowSlotStoreTests</b> - 7/7</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetAllSlotsAsync_ReturnsSlotsAcrossAllPeers</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_WithOwnerScope_ExcludesSlotsWhenNoActivePeerRelationshipExists</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_WithOwnerScope_ReturnsOnlyMatchingOwner</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_ReturnsEmpty_WhenNothingStoredForPeer</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_ReplacesExistingSlots_ForSamePeer</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_ThenGetSlotsAsync_ReturnsSavedSlotsForPeer</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_WithOwnerScope_ReplacesOnlyThatOwnerScope</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ShadowSlotRetentionTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>NewSlotsHaveCreatedAtUtcPopulated</code></td><td>✅</td></tr>
    <tr><td><code>Purge_DeletesOldRows_ButPreservesRecentRows_ForSamePeer</code></td><td>✅</td></tr>
    <tr><td><code>Purge_DeletesRowsOlderThanRetentionWindow</code></td><td>✅</td></tr>
    <tr><td><code>Purge_PreservesRowsWithinRetentionWindow</code></td><td>✅</td></tr>
    <tr><td><code>Purge_WithZeroRetentionDays_ShouldNotBeCalledByService</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerAvailabilitySyncServiceTests</b> - 11/11</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RunSyncCycleAsync_OnOwnerFailure_ContinuesWithNextOwnerAndRecordsFailureMetadata</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_ProcessesAllCalendarOwnersAndStoresAvailabilitySnapshots</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeGoogleWriteBack</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeGraphWriteBack</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeICloudWriteBack</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedGooglePlaceholder</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedGraphPlaceholder</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedICloudPlaceholder</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedGooglePlaceholderUsingConfiguredTitle</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedGraphPlaceholderUsingConfiguredTitle</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedICloudPlaceholderUsingConfiguredTitle</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>InboundPeerPullSyncServiceTests</b> - 10/10</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RunSyncCycleAsync_OnFailure_PreservesPreviouslyStoredSlots</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_OnHttpFailure_RecordsLastSyncedAtAndNotSucceededOnPeerConnection</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_OnSuccess_RecordsLastSyncedAtAndSucceededOnPeerConnection</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_OnSuccess_ReplacesOwnerScopedSlotsAndSendsPeerHeaders</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_SkipsWhenOnlyApiKeyIsConfigured</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_SkipsWhenOnlyInstanceIdIsConfigured</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WhenPeerAFails_StillPullsFromPeerBAndLogsWarning</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WhenPeerIsUnreachable_LogsPeerIdAndFailureReason</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WithInvalidPeerBaseAddress_RecordsFailure</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WithZeroLookAheadDays_ClampsToOne</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>OutboundPeerSyncServiceTests</b> - 8/8</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RunSyncCycleAsync_OnPeerHttpFailure_RecordsLastSyncedAtAndNotSucceededOnPeerConnection</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_PostsCalendarOwnerRefAndBusySlotsWithPeerHeaders</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_PropagatesPeerThumbprintsOnOutgoingRequest</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_UsesOwnerClientObfuscationProfile</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WhenOnePeerFails_ContinuesWithRemainingPeersAndLogsWarning</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WithInvalidBaseAddress_RecordsFailureAndContinues</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WithNoMappings_DoesNotMakeHttpRequest</code></td><td>✅</td></tr>
    <tr><td><code>RunSyncCycleAsync_WithZeroLookAheadDays_StillMakesRequest</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

## Unit (434/434)

<details>
<summary><b>AggregateCalendarSourceTests</b> - 1/1</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>WriteBackSlotsAsync_WritesOtherSourceBusySlotsToEachWritableDestination</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarSourceContractTests</b> - 1/1</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetEventsAsync_ReturnsOnlyEventsWithinRequestedWindow</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>GoogleCalendarSourceCoreTests</b> - 7/7</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetEventsAsync_MapsGoogleResponse</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_RefreshesExpiredToken_BeforeGoogleCall</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_Throws_WhenGoogleApiBaseUrlIsMissing</code></td><td>✅</td></tr>
    <tr><td><code>GetReadinessAsync_ReturnsNotReady_WhenNoCredentialsExist</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_CreatesPlaceholderEventsForInstance</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_ForCalendarOwner_CreatesPlaceholderEventsUsingEnabledGoogleInstance</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_HandlesDuplicateManagedSlotIds_DeletesExtraAndContinues</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>GraphCalendarSourceTests</b> - 17/17</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetEventsAsync_FollowsNextLink_UntilExhausted</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_MapsGraphCalendarViewResponse</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_RefreshesExpiredToken_BeforeGraphCall</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_RetriesGraphRequest_WhenInitialResponseIsUnauthorized</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsEmptyAndLogsWarning_WhenRefreshFails</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReusesRefreshedToken_ForNextLinkRequests</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_SkipsManagedPlaceholderEvents</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_StopsWhenNextLinkRepeats</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_CreatesPlaceholderEvents_ForEachActiveSlot</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_DeletesStaleEvents_WhenNoLongerActiveSlot</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_DoesNotDeleteManagedEvent_WhenStartIsOutsideWindow</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_ForSourceInstance_CreatesPlaceholderEvents</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenReadOnlyConsent</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenScopesAreNull</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_QueriesManagedEventsOnlyWithinWindow</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_SkipsWrite_WhenNoAccessToken</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_UsesCustomPlaceholderTitle</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ICloudCalendarSourceCoreTests</b> - 13/13</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>CreateCalendarQueryRequest_ContainsExpandElement_ForRecurringEventSupport</code></td><td>✅</td></tr>
    <tr><td><code>CreateCalendarQueryRequest_ProducesValidXml</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_AfterAutoMigration_StillReturnsEvents</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithFromAfterTo_ThrowsArgumentException</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithLegacyPlaintextInstanceSecrets_UsesFallbackAndReturnsEvents</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithMissingConfiguration_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithNullCalendarOwnerId_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithUnknownCalendarOwnerId_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>GetReadinessAsync_WithLegacyPlaintextInstanceSecrets_AutoMigratesToProtectedSecretJson</code></td><td>✅</td></tr>
    <tr><td><code>GetReadinessAsync_WithLegacyPlaintextInstanceSecrets_ReturnsReady</code></td><td>✅</td></tr>
    <tr><td><code>GetReadinessAsync_WithNullContextSecretAndLegacyStoredJson_RecoversAndMigrates</code></td><td>✅</td></tr>
    <tr><td><code>GetReadinessAsync_WithOwnerProtectedInstanceSecrets_AutoMigratesToInstanceSecretProtector</code></td><td>✅</td></tr>
    <tr><td><code>GetReadinessAsync_WithProtectedBlobAndPlaintextContext_DoesNotRemigrate</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ICloudCalendarSourceCoreWriteBackTests</b> - 22/22</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>BuildPlaceholderIcsContent_ContainsRequiredICalFields</code></td><td>✅</td></tr>
    <tr><td><code>BuildPlaceholderIcsContent_UsesCrlfLineEndings</code></td><td>✅</td></tr>
    <tr><td><code>GetManagedEventUid_DifferentSlotIdsProduceDifferentUids</code></td><td>✅</td></tr>
    <tr><td><code>GetManagedEventUid_SameSlotIdProducesSameUid</code></td><td>✅</td></tr>
    <tr><td><code>GetManagedEventUid_StartsWithObfusCPrefix</code></td><td>✅</td></tr>
    <tr><td><code>ParseManagedCalDavEvents_EmptyBody_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>ParseManagedCalDavEvents_EventWithoutManagedMarker_IsExcluded</code></td><td>✅</td></tr>
    <tr><td><code>ParseManagedCalDavEvents_ManagedEvent_IsIncludedWithCorrectSlotId</code></td><td>✅</td></tr>
    <tr><td><code>ParseManagedCalDavEvents_MixedEvents_FiltersOutNonManaged</code></td><td>✅</td></tr>
    <tr><td><code>ResolveCalDavHref_WithAbsoluteHttpUrl_UsesItDirectly</code></td><td>✅</td></tr>
    <tr><td><code>ResolveCalDavHref_WithAbsoluteHttpsUrl_UsesItDirectly</code></td><td>✅</td></tr>
    <tr><td><code>ResolveCalDavHref_WithAbsolutePath_ResolvesAgainstServerOrigin</code></td><td>✅</td></tr>
    <tr><td><code>ResolveCalDavHref_WithRelativePathNoLeadingSlash_PrependsMissingSlash</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_CreatesPlaceholderEventsViaCalDavPut</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_DeletesStaleManagedEvents</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_InstanceBased_WithMissingConfig_DoesNotMakeHttpRequests</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_KeepsManagedEventWhenSlotIsStillActive</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_PutRequestContainsManagedMarkerAndSlotId</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_SkipsPut_WhenSlotIsAlreadyUpToDate</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_WithMissingICloudConfiguration_DoesNotMakeHttpRequests</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_WithUnknownOwner_DoesNotMakeHttpRequests</code></td><td>✅</td></tr>
    <tr><td><code>WriteBackSlotsAsync_WritesCorrectStartAndEnd</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>IcalFeedCalendarSourceTests</b> - 34/34</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetEventsAsync_AggregatesEventsFromMultipleFeeds</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_AllDayEventWithSameDateDtstartDtend_NormalizesToOneDay</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ContinuesWithOtherFeeds_WhenOneFeedFails</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_DoesNotThrow_WhenFromEqualsTo</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ExcludesEventEndingExactlyAtFrom</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ExcludesEventStartingExactlyAtTo</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_FallsBackToMockSource_WhenNoFeedsConfigured</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesAllDayEventWithValueDateAndExplicitDtend</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesAttendeeWithMailtoPrefix_StripsPrefix</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesAttendeeWithoutMailtoPrefix_KeepsOriginal</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesDateOnlyDtstart</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesDescription</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesDtstartWithTzidAmsterdam_ConvertsToUtc</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesDtstartWithUnknownTzid_FallsBackToFloatingUtc</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesDtstartWithoutZ_AssumedUtc</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesEventWithoutDescription_ReturnsNullDescription</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesEventWithoutSummary_DefaultsToBusy</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesEventWithoutUid_GeneratesId</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesLocation</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ParsesPropertyWithSemicolonParameter</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_RejectsEventWithEndBeforeOrEqualStart</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsEmptyForNonExistentOwner</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsEmptyList_WhenAllFeedsFail</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsEventsOrderedByStartAscending</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsParsedEventsWithinRequestedWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_SkipsLineWithColonAtPosition0</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ThrowsArgumentException_WhenFromIsAfterTo</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ThrowsOperationCanceled_WhenTokenCancelled</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithEmptyIcsContent_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithInvalidFeedUrl_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithLineFolding_ParsesContinuationLines</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithNullOwnerId_FallsBackToMockSource</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithTabLineFolding_ParsesContinuationLines</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithoutDtend_DateTimeStart_DefaultsTo30Minutes</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>IcsCalendarEventParserTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ParseEvents_AssignsUniqueIds_ToRecurringEventOccurrences_WithRecurrenceId</code></td><td>✅</td></tr>
    <tr><td><code>ParseEvents_IncludesRegularEventsAlongside_WhenMixedWithManaged</code></td><td>✅</td></tr>
    <tr><td><code>ParseEvents_MasterOccurrence_UsesUidWithoutSuffix_WhenNoRecurrenceId</code></td><td>✅</td></tr>
    <tr><td><code>ParseEvents_SkipsEventsWithObfusCal_ManagedFlag</code></td><td>✅</td></tr>
    <tr><td><code>ParseEvents_SkipsEventsWithObfusCal_ManagedFlag_CaseInsensitive</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>MockCalendarSourceTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Application_ResolvesMockCalendarSource_AsActiveCalendarSource_InDevelopment</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsAtLeastOneEventWithSensitiveFieldsPopulated</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ReturnsOnlyEventsInsideRequestedWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_ThrowsOperationCanceledException_WhenCancelled</code></td><td>✅</td></tr>
    <tr><td><code>GetEventsAsync_WithFourteenDayWindowStartingToday_ReturnsAtLeastThreeEvents</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ConsentCallbackTests</b> - 21/21</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ConsentCallback_DisablesPrerender</code></td><td>✅</td></tr>
    <tr><td><code>ConsentCallback_DoesNotDirectlyInjectConsentServices</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Authorization code is required to complete Google consent.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Calendar owner was not found.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Cannot access a disposed context instance. A common cause of this error is disposing a context instance that was resolved from dependency injection and then later trying to use the same context instance elsewhere in your application. This may occur if you are calling 'Dispose' on the context instance, or wrapping it in a using statement. If you are using dependency injection, you should let the dependency injection container take care of disposing context instances.
Object name: 'AppDbContext'.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Google calendar source instance was not found.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Google token exchange failed with HTTP 400: error='access_denied', description='User denied'.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Google token exchange failed with invalid_grant. The authorization code may be expired or already used, or the redirect URI did not exactly match the URI used during authorization.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Graph calendar source instance was not found.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("The specified calendar source instance is not a Google source.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state does not match this calendar source.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state has expired. Start consent again.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state is invalid or expired.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state is invalid.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state does not contain owner context. Use the instance-specific consent flow.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state has expired. Start consent again.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state is invalid or expired.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state is invalid.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("State is required to complete Google consent.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("State is required to complete Graph consent.")</code></td><td>✅</td></tr>
    <tr><td><code>IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("state is required to complete anything")</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ApplicationDependencyInjectionTests</b> - 8/8</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>AddApplication_RegistersAllFiveEventTransformers</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersMergeBlocksTransformer</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersObfuscationPipeline</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersRemoveAttendeesTransformer</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersRemoveDescriptionTransformer</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersRemoveLocationTransformer</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersRemoveTitleTransformer</code></td><td>✅</td></tr>
    <tr><td><code>AddApplication_RegistersRoundTimesTransformer</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ConfigurationDefaultsTests</b> - 4/4</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GraphConsentOptions_HasExpectedDefaults</code></td><td>✅</td></tr>
    <tr><td><code>PeerConnection_ApiKeyHash_DefaultsToEmpty</code></td><td>✅</td></tr>
    <tr><td><code>PeerTransportSecurityOptions_DefaultsToRejectingSelfSignedCertificates</code></td><td>✅</td></tr>
    <tr><td><code>SyncOptions_HasExpectedDefaults</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>DefaultLogRedactorTests</b> - 2/2</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Redact_MasksBearerTokenAndApiKey</code></td><td>✅</td></tr>
    <tr><td><code>Redact_MasksConnectionStringPassword</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>SecretStartupValidatorTests</b> - 2/2</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ValidateOrThrow_DoesNotThrow_WhenAllRequiredSecretsExist</code></td><td>✅</td></tr>
    <tr><td><code>ValidateOrThrow_Throws_WhenRequiredSecretIsMissing</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ObfuscationPipelineLoggingTests</b> - 2/2</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Process_EmitsAuditLog_WithRequiredStructuredFields_AndNoSensitiveFields</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithEmptyEventList_StillEmitsAuditLog</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ObfuscationPipelineTests</b> - 25/25</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Process_KeepsSeparateSlots</code></td><td>✅</td></tr>
    <tr><td><code>Process_MergesAdjacentSlots</code></td><td>✅</td></tr>
    <tr><td><code>Process_MergesMultipleOverlappingSlots</code></td><td>✅</td></tr>
    <tr><td><code>Process_MergesOverlappingSlots</code></td><td>✅</td></tr>
    <tr><td><code>Process_MergesWithRoundingTransformer</code></td><td>✅</td></tr>
    <tr><td><code>Process_TransformersAppliedInRegistrationOrder</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithCustomRoundingInterval_UsesProfileInterval</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithEmptyConsultantId_ThrowsArgumentException</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithEmptyEventList_ReturnsEmptyList</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithExplicitProfile_UsesProvidedProfile</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithFullPipeline_OutputContainsNoSensitiveFields</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithFullPipeline_PreservesSourceEventId</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithFullPipeline_ReturnsBusySlotsWithCorrectTimeWindow</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithIEnumerableEvents_StillWorks</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithMultipleEvents_ReturnsOneSlotPerEvent</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithNone_StillProducesBusySlotsWithCorrectWindow</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithNullConsultantId_ThrowsArgumentException</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithNullProfile_UsesDefaultProfile</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithProfileDisablingAttendees_KeepsAttendees</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithProfileDisablingDescription_KeepsDescription</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithProfileDisablingLocation_KeepsLocation</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithProfileDisablingMergeBlocks_KeepsSeparateSlots</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithProfileDisablingRoundTimes_KeepsOriginalTimes</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithProfileDisablingTitle_KeepsTitle</code></td><td>✅</td></tr>
    <tr><td><code>Process_WithWhitespaceConsultantId_ThrowsArgumentException</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>MergeBlocksTransformerTests</b> - 13/13</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Transform_PreservesObfuscatedDataInSourceSlots</code></td><td>✅</td></tr>
    <tr><td><code>Transform_SortsUnsortedInputBeforeMerging</code></td><td>✅</td></tr>
    <tr><td><code>Transform_ThreeSlots_FirstTwoMerge_ThirdSeparate</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithAdjacentSlots_MergesIntoOne</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithContainedSlot_PreservesOuterEnd</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithEmptyList_ReturnsEmptyList</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithIdenticalEndTimes_MergesCorrectly</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithMergedSlots_CapturesAllSourceSlots</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithNonOverlappingSlots_KeepsBoth</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithOverlapping_WhereSecondEndsEarlier_KeepsFirstEnd</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithOverlapping_WhereSecondEndsLater_TakesSecondEnd</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithSingleSlot_ReturnsThatSlot</code></td><td>✅</td></tr>
    <tr><td><code>Transform_WithThreeSlots_FirstTwoMerged_CapturesSourcesCorrectly</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>RemoveAttendeesTransformerTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RemoveAttendeesTransformer_ClearsAttendeeEmails</code></td><td>✅</td></tr>
    <tr><td><code>RemoveAttendeesTransformer_PreservesDescription</code></td><td>✅</td></tr>
    <tr><td><code>RemoveAttendeesTransformer_PreservesLocation</code></td><td>✅</td></tr>
    <tr><td><code>RemoveAttendeesTransformer_PreservesTimeWindow</code></td><td>✅</td></tr>
    <tr><td><code>RemoveAttendeesTransformer_PreservesTitle</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>RemoveDescriptionTransformerTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RemoveDescriptionTransformer_ClearsDescription</code></td><td>✅</td></tr>
    <tr><td><code>RemoveDescriptionTransformer_DoesNotModifyAttendees</code></td><td>✅</td></tr>
    <tr><td><code>RemoveDescriptionTransformer_DoesNotModifyLocation</code></td><td>✅</td></tr>
    <tr><td><code>RemoveDescriptionTransformer_PreservesTimeWindow</code></td><td>✅</td></tr>
    <tr><td><code>RemoveDescriptionTransformer_PreservesTitle</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>RemoveLocationTransformerTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RemoveLocationTransformer_ClearsLocation</code></td><td>✅</td></tr>
    <tr><td><code>RemoveLocationTransformer_DoesNotModifyAttendees</code></td><td>✅</td></tr>
    <tr><td><code>RemoveLocationTransformer_PreservesDescription</code></td><td>✅</td></tr>
    <tr><td><code>RemoveLocationTransformer_PreservesTimeWindow</code></td><td>✅</td></tr>
    <tr><td><code>RemoveLocationTransformer_PreservesTitle</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>RemoveTitleTransformerTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RemoveTitleTransformer_ClearsTitle</code></td><td>✅</td></tr>
    <tr><td><code>RemoveTitleTransformer_DoesNotModifyAttendees</code></td><td>✅</td></tr>
    <tr><td><code>RemoveTitleTransformer_DoesNotModifyLocation</code></td><td>✅</td></tr>
    <tr><td><code>RemoveTitleTransformer_PreservesDescription</code></td><td>✅</td></tr>
    <tr><td><code>RemoveTitleTransformer_PreservesTimeWindow</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>RoundTimesTransformerTests</b> - 22/22</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>RoundTimesTransformer_Arithmetic_WithOneMinuteInterval</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_EndExactlyAtMidnight_StaysAtMidnight</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_EndJustBeforeMidnight_RoundsUpToMidnight</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_EndJustPastMidnight_RoundsToNextBoundary</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_HandlesSpecialCase30Minutes</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_PreservesAttendees</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_PreservesDescription</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_PreservesId</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_PreservesLocation</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_PreservesTitle</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundDown_JustPastBoundary</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundUp_ExactlyOnBoundary_StaysSame</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundUp_MidnightNonZeroRemainder</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundsAlignedTimesUnchanged</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundsEndTimeCrossingMidnight_ToStartOfNextDay</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundsEndTimeUp</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundsStartTimeDown</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_RoundsStartTo15MinuteBoundary</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_WithCustomInterval_RoundsCorrectly</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_WithNegativeInterval_ThrowsArgumentOutOfRange</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_WithPositiveInterval_DoesNotThrow</code></td><td>✅</td></tr>
    <tr><td><code>RoundTimesTransformer_WithZeroInterval_ThrowsArgumentOutOfRange</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerGoogleConsentServiceTests</b> - 4/4</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>BuildAuthorizationUrlAsync_IncludesSelectAccountPrompt</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrlAsync_UsesConfiguredRedirectUri_WhenPresent</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrlAsync_WithLocalDomainRedirectUri_ThrowsClearException</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentFromStateAsync_UsesRedirectUriStoredInState</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerGraphConsentServiceTests</b> - 27/27</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>BuildAuthorizationUrlAsync_IncludesStatePrefixedWithGraph</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_ContainsResponseModeQuery</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_EmptyAuthorityTenant_FallsBackToAzureAdTenantId</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_FallsBackToAzureAdClientId_WhenConsentClientIdIsNull</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_IncludesPromptConsent</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_ReturnsValidUrl</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_TrimsTrailingSlashFromInstance</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_UsesConsentClientId</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_UsesCustomScope_WhenScopeIsConfigured</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_UsesDefaultScope_WhenScopeIsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>BuildAuthorizationUrl_WithRelativeUri_Throws</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentAsync_StoresTokensAndUpdatesTimestamps</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentAsync_ThenGetStatus_ShowsConsent</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentAsync_ThenHasConsent_ReturnsTrue</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentAsync_WithInvalidOwner_Throws</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentAsync_WithNullRefreshToken_StoresNull</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentAsync_WithWhitespaceRefreshToken_StoresNull</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentFromStateAsync_RoundTrip_CompletesConsent</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentFromStateAsync_WithInvalidToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentFromStateAsync_WithLegacyNonOwnerState_Throws</code></td><td>✅</td></tr>
    <tr><td><code>CompleteConsentFromStateAsync_WithoutGraphPrefix_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetStatusAsync_ReturnsNoConsent_ForNewOwner</code></td><td>✅</td></tr>
    <tr><td><code>GetStatusAsync_ReturnsNull_WhenOwnerDoesNotExist</code></td><td>✅</td></tr>
    <tr><td><code>GetStatusAsync_WithAccessTokenOnly_ReturnsHasConsent</code></td><td>✅</td></tr>
    <tr><td><code>HasConsentAsync_ReturnsFalse_ForNewOwner</code></td><td>✅</td></tr>
    <tr><td><code>HasConsentAsync_WithRefreshTokenOnly_ReturnsTrue</code></td><td>✅</td></tr>
    <tr><td><code>HasConsentAsync_WithTokensExplicitlyCleared_ReturnsFalse</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerICloudConfigurationServiceTests</b> - 6/6</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ClearConfigurationAsync_DisablesAndClearsInstancePayload</code></td><td>✅</td></tr>
    <tr><td><code>GetConfigurationAsync_MasksStoredAppleId</code></td><td>✅</td></tr>
    <tr><td><code>GetConfigurationAsync_WithUnconfiguredIcloudInstance_ReturnsConfiguredFalse</code></td><td>✅</td></tr>
    <tr><td><code>GetConfigurationAsync_WithUnknownOwner_ReturnsNull</code></td><td>✅</td></tr>
    <tr><td><code>SetConfigurationAsync_CreatesAndStoresIcloudInstanceConfiguration</code></td><td>✅</td></tr>
    <tr><td><code>SetConfigurationAsync_WithUnknownOwner_ReturnsNull</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerIcalFeedServiceTests</b> - 14/14</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>AddFeedAsync_ReturnsAdded_OnSuccess</code></td><td>✅</td></tr>
    <tr><td><code>AddFeedAsync_ReturnsCalendarOwnerNotFound_WhenOwnerMissing</code></td><td>✅</td></tr>
    <tr><td><code>AddFeedAsync_ReturnsDuplicate_WhenSameUrlAddedTwice</code></td><td>✅</td></tr>
    <tr><td><code>AddFeedAsync_ReturnsInvalidUrl_WhenUrlValidatorRejectsInput</code></td><td>✅</td></tr>
    <tr><td><code>AddFeedAsync_ReturnsNotFound_WhenDifferentOwnerIdProvided</code></td><td>✅</td></tr>
    <tr><td><code>DeleteFeedAsync_DoesNotDeleteOtherOwnersFeed</code></td><td>✅</td></tr>
    <tr><td><code>DeleteFeedAsync_RequiresBothFeedIdAndOwnerIdToMatch</code></td><td>✅</td></tr>
    <tr><td><code>DeleteFeedAsync_ReturnsCalendarOwnerNotFound_WhenOwnerMissing</code></td><td>✅</td></tr>
    <tr><td><code>DeleteFeedAsync_ReturnsDeleted_OnSuccess</code></td><td>✅</td></tr>
    <tr><td><code>DeleteFeedAsync_ReturnsFeedNotFound_WhenFeedDoesNotExist</code></td><td>✅</td></tr>
    <tr><td><code>DeleteFeedAsync_ReturnsNotFound_WhenDifferentOwnerIdProvided</code></td><td>✅</td></tr>
    <tr><td><code>ListFeedsAsync_DoesNotReturnOtherOwnersFeeds</code></td><td>✅</td></tr>
    <tr><td><code>ListFeedsAsync_ReturnsAddedFeeds</code></td><td>✅</td></tr>
    <tr><td><code>ListFeedsAsync_ReturnsEmpty_WhenNoFeeds</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerObfuscationProfileServiceTests</b> - 16/16</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>EnsureDefaultProfiles_CreatedWithSecureValues</code></td><td>✅</td></tr>
    <tr><td><code>GetProfileAsync_ForExistingOwner_ReturnsSavedProfile</code></td><td>✅</td></tr>
    <tr><td><code>GetProfileAsync_ForNonExistentOwner_ReturnsDefault</code></td><td>✅</td></tr>
    <tr><td><code>GetProfileAsync_ReturnsDefaultForNewOwner</code></td><td>✅</td></tr>
    <tr><td><code>GetProfileAsync_ReturnsDefault_WhenOwnerDoesNotExist</code></td><td>✅</td></tr>
    <tr><td><code>GetProfilesAsync_AutoCreatesDefaultProfiles</code></td><td>✅</td></tr>
    <tr><td><code>GetProfilesAsync_DoesNotDuplicate_OnRepeatedCalls</code></td><td>✅</td></tr>
    <tr><td><code>GetProfilesAsync_ReturnedInCorrectOrder_NotDescending</code></td><td>✅</td></tr>
    <tr><td><code>GetProfilesAsync_ReturnsSortedByContext</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_DoesNotAffectOtherContext</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_ForNonExistentOwner_DoesNotThrow</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_OnNewOwner_AutoCreatesAndUpdates</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_PersistsChanges</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_UpdatesExistingProfile</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_WithNegativeInterval_ThrowsArgumentOutOfRange</code></td><td>✅</td></tr>
    <tr><td><code>SetProfileAsync_WithZeroInterval_ThrowsArgumentOutOfRange</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>ObfuscationProfileDefaultsTests</b> - 4/4</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>NewObfuscationProfile_HasSecureDefaults</code></td><td>✅</td></tr>
    <tr><td><code>NewObfuscationProfile_PropertiesCanBeModified</code></td><td>✅</td></tr>
    <tr><td><code>ObfuscationProfileSettings_CreateDefault_HasSecureDefaults</code></td><td>✅</td></tr>
    <tr><td><code>ObfuscationProfileSettings_CreateDefault_InternalContext</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarSourcePluginCatalogTests</b> - 10/10</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Discover_FindsAllThreeBuiltInPlugins</code></td><td>✅</td></tr>
    <tr><td><code>Discover_IgnoresCalendarSourceTypes_WithoutAttribute</code></td><td>✅</td></tr>
    <tr><td><code>Discover_MarksBuiltInPlugins_AsNotExternal</code></td><td>✅</td></tr>
    <tr><td><code>Discover_ProvidesActionMetadata_ForPluginsWithConsentFlows</code></td><td>✅</td></tr>
    <tr><td><code>Discover_ProvidesUiMetadata_ForBuiltInPlugins</code></td><td>✅</td></tr>
    <tr><td><code>Discover_ReturnsDistinctPluginIds</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugin_FindsById_CaseInsensitively</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugin_ReturnsNull_ForNullOrWhitespace</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugin_ReturnsNull_ForUnknownId</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugins_ReturnsAllDescriptors_InAlphabeticalOrder</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarSourceResolverTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ResolveAsync_FallsBackToFirstPlugin_WhenConfiguredProviderNotInCatalog</code></td><td>✅</td></tr>
    <tr><td><code>ResolveAsync_UsesConfiguredProvider_WhenNoOwnerIdProvided</code></td><td>✅</td></tr>
    <tr><td><code>ResolveAsync_UsesConfiguredProvider_WhenOwnerHasNoSelection</code></td><td>✅</td></tr>
    <tr><td><code>ResolveAsync_UsesConfiguredProvider_WhenOwnerIdNotInDatabase</code></td><td>✅</td></tr>
    <tr><td><code>ResolveAsync_UsesOwnerPluginId_WhenOwnerHasExplicitSelection</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>GoogleICloudPluginIntegrationTests</b> - 3/3</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GoogleAndICloudDlls_ShouldExistInPluginFolder</code></td><td>✅</td></tr>
    <tr><td><code>GoogleICloudPlugins_ShouldBeDiscovered_WhenAssembliesAreLoaded</code></td><td>✅</td></tr>
    <tr><td><code>PluginFolder_ShouldExist</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PluginAllowlistTests</b> - 14/14</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Cache_AfterInitialize_ContainsBlockedIds</code></td><td>✅</td></tr>
    <tr><td><code>Cache_IsCaseInsensitive</code></td><td>✅</td></tr>
    <tr><td><code>Cache_IsNotInitialized_BeforeInitializeIsCalled</code></td><td>✅</td></tr>
    <tr><td><code>Cache_MarkAllowed_RemovesEntry</code></td><td>✅</td></tr>
    <tr><td><code>Cache_MarkBlocked_AddsEntry</code></td><td>✅</td></tr>
    <tr><td><code>Discover_AllowsBuiltInPlugins_WhenAllowlistDisabled</code></td><td>✅</td></tr>
    <tr><td><code>Discover_AllowsBuiltInPlugins_WhenAllowlistEnabled</code></td><td>✅</td></tr>
    <tr><td><code>Discover_DoesNotThrow_WhenAllowlistOptionsIsNull</code></td><td>✅</td></tr>
    <tr><td><code>Discover_DoesNotThrow_WhenSomeAssemblyTypesCannotBeLoaded</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugin_ReturnsNull_ForBlockedPlugin</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugin_ReturnsPlugin_WhenNotBlocked</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugins_FiltersBlockedPlugins_ViaCache</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugins_ReflectsRuntimeToggle_ImmediatelyAfterCacheUpdate</code></td><td>✅</td></tr>
    <tr><td><code>GetPlugins_ReturnsAll_WhenCacheIsNull</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PluginLoadingTests</b> - 1/1</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>SimulatesStartup_LoadsAndDiscoversGoogleAndICloudPlugins</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>AesGcmColumnEncryptorTests</b> - 7/7</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Constructor_ThrowsInvalidOperation_WhenKeyIsTooShort</code></td><td>✅</td></tr>
    <tr><td><code>Constructor_ThrowsInvalidOperation_WhenSecretMissing</code></td><td>✅</td></tr>
    <tr><td><code>Decrypt_RoundTrips_Correctly</code></td><td>✅</td></tr>
    <tr><td><code>Decrypt_WithTamperedTag_ThrowsCryptographicException</code></td><td>✅</td></tr>
    <tr><td><code>Encrypt_EmptyString_RoundTrips</code></td><td>✅</td></tr>
    <tr><td><code>Encrypt_ProducesCiphertextDifferentFromPlaintext</code></td><td>✅</td></tr>
    <tr><td><code>Encrypt_SamePlaintext_ProducesDifferentCiphertexts_PerCall</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PeerApiKeySecurityTests</b> - 3/3</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Hash_ProducesPbkdf2FormattedHash_AndVerifyPasses</code></td><td>✅</td></tr>
    <tr><td><code>Verify_WithDifferentKey_Fails</code></td><td>✅</td></tr>
    <tr><td><code>Verify_WithLegacySha256Hash_RemainsSupported</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PeerTransportSecurityTests</b> - 4/4</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>NormalizeThumbprint_StripsWhitespaceAndSeparators</code></td><td>✅</td></tr>
    <tr><td><code>ValidateRemoteCertificate_AcceptsPinnedCertificate_EvenWhenChainErrorsExist</code></td><td>✅</td></tr>
    <tr><td><code>ValidateRemoteCertificate_AcceptsSelfSignedCertificate_WhenExplicitlyAllowed</code></td><td>✅</td></tr>
    <tr><td><code>ValidateRemoteCertificate_RejectsMismatchedPinnedCertificate</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>UrlSafetyValidatorTests</b> - 3/3</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ValidateAsync_ReturnsInvalid_ForHttpScheme</code></td><td>✅</td></tr>
    <tr><td><code>ValidateAsync_ReturnsInvalid_ForPrivateIpAddress</code></td><td>✅</td></tr>
    <tr><td><code>ValidateAsync_ReturnsValid_ForPublicHttpsUrl</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>EfCoreShadowSlotStoreInMemoryTests</b> - 17/17</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetAllSlotsAsync_ExcludesSlotEndingExactlyAtFrom</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_FiltersByTimeWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_IncludesSlotAtExactFromBoundary</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_IncludesSlotEndingAtToBoundary</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_MapsAllFieldsFromUnscoped</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OnlyReturnsUnscoped_NotOwnerScoped</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_ExcludesSlotEndingExactlyAtFrom</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_ExcludesSlotStartingExactlyAtTo</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_FiltersByOwnerAndTimeWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_OwnerScoped_WithNullPeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_UnknownPeer_ReturnsEmpty</code></td><td>✅</td></tr>
    <tr><td><code>SetAndGet_MapsAllFields</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_ReplacesOnlyForThatOwner</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_ReplacesExistingSlotsForSamePeer</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_WithEmptyPeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_WithNullSlots_Throws</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>EfCoreShadowSlotStoreLoggingTests</b> - 7/7</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetAllSlotsAsync_EmitsLogWithSlotCount</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_OwnerScoped_WithWhitespacePeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_WithWhitespacePeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_EmitsLogWithPeerIdAndCount</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_WithWhitespacePeerId_Throws</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>InMemoryShadowSlotStoreLoggingTests</b> - 5/5</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetAllSlotsAsync_EmitsLogWithPeerCountAndSlotCount</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_EmitsLogWithOwnerIdAndSlotCount</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_EmitsLogWithPeerIdAndCount</code></td><td>✅</td></tr>
    <tr><td><code>SetAndGetSlots_EmitStructuredLogs_WithPeerIdAndCountOnly</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdAndOwnerIdAndCount</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>InMemoryShadowSlotStoreTests</b> - 34/34</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>GetAllSlotsAsync_ExcludesSlotEntirelyAfterWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_ExcludesSlotEntirelyBeforeWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_ExcludesSlotStartingExactlyAtTo</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_IncludesSlotEndingAfterToButOverlapping</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_IncludesSlotExactlyAtFromBoundary</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_IncludesSlotExactlyAtToBoundary</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_IncludesSlotStartingBeforeFromButOverlapping</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_AggregatesFromMultiplePeers</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_ExcludesSlotEntirelyAfterWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_ExcludesSlotEntirelyOutsideWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_FiltersToCorrectOwner</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_IncludesSlotFullyInsideWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingEnd</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingStart</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_IncludesSlotSpanningEntireWindow</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_OwnerScoped_WithCancelledToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_ReturnsEmptyArray_WhenNoSlotsAreStored</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_ReturnsSlotsAfterReplacingPeerSlots</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_ReturnsSlotsFromMultiplePeers</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_ReturnsSlotsFromSinglePeer</code></td><td>✅</td></tr>
    <tr><td><code>GetAllSlotsAsync_WithCancelledToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_OwnerScoped_UnknownPeerOwner_ReturnsEmptyList</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_OwnerScoped_WithCancelledToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_UnknownPeer_ReturnsEmptyList</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_WithCancelledToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>GetSlotsAsync_WithNullPeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_AndGetSlotsAsync_AreThreadSafeUnderConcurrentAccess</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_ForDifferentPeers_KeepsDataIsolated</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_WithCancelledToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_WithNullPeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_OwnerScoped_WithNullSlots_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_WithCancelledToken_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_WithNullPeerId_Throws</code></td><td>✅</td></tr>
    <tr><td><code>SetSlotsAsync_WithWhitespacePeerId_Throws</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>CalendarOwnerAvailabilityBackgroundServiceTests</b> - 1/1</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>StartAsync_InvokesCalendarOwnerAvailabilitySyncService</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PeerSyncBackgroundServiceTests</b> - 7/7</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>ExecuteAsync_ContinuesAfterSyncFailure</code></td><td>✅</td></tr>
    <tr><td><code>ExecuteAsync_RunsMultipleCycles_WithSmallInterval</code></td><td>✅</td></tr>
    <tr><td><code>ExecuteAsync_UsesConfiguredInterval_ClampedToMinimum</code></td><td>✅</td></tr>
    <tr><td><code>ExecuteAsync_WithLargeInterval_DoesNotRunSecondCycleQuickly</code></td><td>✅</td></tr>
    <tr><td><code>ExecuteAsync_WithNegativeInterval_ClampsToOneSecond</code></td><td>✅</td></tr>
    <tr><td><code>StartAsync_InvokesOutboundAndInboundSyncServices</code></td><td>✅</td></tr>
    <tr><td><code>StopAsync_StopsGracefully</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>GetBusySlotsQueryHandlerTests</b> - 6/6</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Handle_MapsAllFieldsToResponse</code></td><td>✅</td></tr>
    <tr><td><code>Handle_ReturnsObfuscatedBusySlots</code></td><td>✅</td></tr>
    <tr><td><code>Handle_ThrowsRequestValidationException_WhenWindowExceedsConfiguredLimit</code></td><td>✅</td></tr>
    <tr><td><code>Handle_UsesClientContext</code></td><td>✅</td></tr>
    <tr><td><code>Handle_WithMultipleEvents_ReturnsOneResponsePerEvent</code></td><td>✅</td></tr>
    <tr><td><code>Handle_WithNoEvents_ReturnsEmptyList</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>GetMergedFreeBusyQueryHandlerTests</b> - 6/6</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Handle_CombinesOwnAndShadowSlots_InSortedOrder</code></td><td>✅</td></tr>
    <tr><td><code>Handle_PrefersPersistedAvailabilitySlots_WhenAvailable</code></td><td>✅</td></tr>
    <tr><td><code>Handle_ReturnsOwnSlotsWhenNoShadowSlots</code></td><td>✅</td></tr>
    <tr><td><code>Handle_ReturnsShadowSlotsWhenNoOwnEvents</code></td><td>✅</td></tr>
    <tr><td><code>Handle_SortsCombinedSlots_ShadowBeforeOwn</code></td><td>✅</td></tr>
    <tr><td><code>Handle_UsesInternalContext_ForObfuscation</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

<details>
<summary><b>PushShadowSlotsCommandHandlerTests</b> - 6/6</summary>

<table>
  <thead>
    <tr><th>Test</th><th>Result</th></tr>
  </thead>
  <tbody>
    <tr><td><code>Handle_CreatesCorrectSourceEventId_WithIndex</code></td><td>✅</td></tr>
    <tr><td><code>Handle_DeduplicatesOwnerIds</code></td><td>✅</td></tr>
    <tr><td><code>Handle_MapsAllSlotFields</code></td><td>✅</td></tr>
    <tr><td><code>Handle_StoresSlotsForEachDistinctOwner</code></td><td>✅</td></tr>
    <tr><td><code>Handle_StoresSlotsWithPeerIdPrefix</code></td><td>✅</td></tr>
    <tr><td><code>Handle_ThrowsRequestValidationException_WhenBatchExceedsLimit</code></td><td>✅</td></tr>
  </tbody>
</table>

</details>

