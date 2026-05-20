# Test Catalog
_Auto-generated from CI run. 578 tests across 62 classes._

## Integration (172/172)

<details>
<summary><b>AdminPeerConnectionsControllerTests</b> - 7/7</summary>

| Test | Result |
|------|--------|
| `AdminEndpoints_EnforceRoleAndAuthentication` | ✅ |
| `ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash` | ✅ |
| `Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesHttpScheme` | ✅ |
| `Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesPrivateIpHost` | ✅ |
| `RevokeFlow_SetsRevokedAt_AndPeerAuthStopsImmediately` | ✅ |
| `RotateFlow_InvalidatesOldApiKeyAndActivatesNewApiKey` | ✅ |
| `SuspendFlow_SetsStatusToSuspended_AndAuthStopsImmediately` | ✅ |

</details>

<details>
<summary><b>CalendarOwnersControllerIcalFeedsTests</b> - 12/12</summary>

| Test | Result |
|------|--------|
| `AddIcalFeed_DoesNotChangePlugin_WhenOwnerAlreadyUsesNonMockProvider` | ✅ |
| `AddIcalFeed_ReturnsBadRequest_WhenFeedUrlIsInvalid` | ✅ |
| `AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesHttpScheme` | ✅ |
| `AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesPrivateIpHost` | ✅ |
| `AddIcalFeed_ReturnsConflict_WhenFeedAlreadyExists` | ✅ |
| `AddIcalFeed_ReturnsCreated_AndPersistsFeed` | ✅ |
| `AddIcalFeed_ReturnsUnauthorized_WithoutToken` | ✅ |
| `AddIcalFeed_ReturnsValidationProblemDetails_WhenFeedUrlIsMissing` | ✅ |
| `AddIcalFeed_SwitchesCalendarSourcePluginToIcal_WhenOwnerUsesPlaceholderProvider` | ✅ |
| `DeleteIcalFeed_ReturnsNoContent_AndRemovesFeed` | ✅ |
| `DeleteIcalFeed_ReturnsNotFound_WhenFeedDoesNotExist` | ✅ |
| `ListIcalFeeds_ReturnsOk_WithConfiguredFeeds` | ✅ |

</details>

<details>
<summary><b>CalendarOwnersControllerTests</b> - 37/37</summary>

| Test | Result |
|------|--------|
| `CompleteCalendarConsent_ReturnsBadRequest_WhenAuthorizationCodeIsInvalid` | ✅ |
| `CompleteCalendarConsent_ReturnsBadRequest_WhenAuthorizationCodeMissing` | ✅ |
| `CompleteCalendarConsent_ReturnsBadRequest_WhenRedirectUriInvalid` | ✅ |
| `GetBusySlots_BadRequest_ContainsMeaningfulMessage` | ✅ |
| `GetBusySlots_ReturnsBadRequest_WhenFromIsInvalid` | ✅ |
| `GetBusySlots_ReturnsBadRequest_WhenFromIsMissing` | ✅ |
| `GetBusySlots_ReturnsBadRequest_WhenToIsInvalid` | ✅ |
| `GetBusySlots_ReturnsBadRequest_WhenToIsMissing` | ✅ |
| `GetBusySlots_ReturnsBadRequest_WhenWindowExceedsConfiguredMaximum` | ✅ |
| `GetBusySlots_ReturnsConflict_WhenCalendarOwnerHasNotGrantedGraphConsent` | ✅ |
| `GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId` | ✅ |
| `GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId` | ✅ |
| `GetBusySlots_ReturnsOk_WithValidParameters` | ✅ |
| `GetBusySlots_ReturnsUnauthorized_WithoutToken` | ✅ |
| `GetCalendarConsentStatus_ReturnsFalseBeforeConsent_AndTrueAfterCompletion` | ✅ |
| `GetCalendarConsentUrl_ReturnsAuthorizationUrl_ForAuthenticatedCalendarOwner` | ✅ |
| `GetCalendarConsentUrl_ReturnsBadRequest_WhenRedirectUriIsRelative` | ✅ |
| `GetCalendarConsentUrl_ReturnsBadRequest_WhenRedirectUriMissing` | ✅ |
| `GetCurrentCalendarOwner_ReturnsAuthenticatedObjectId` | ✅ |
| `GetMergedFreeBusy_BadRequest_ContainsMeaningfulMessage` | ✅ |
| `GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsInvalid` | ✅ |
| `GetMergedFreeBusy_ReturnsBadRequest_WhenFromIsMissing` | ✅ |
| `GetMergedFreeBusy_ReturnsBadRequest_WhenToIsInvalid` | ✅ |
| `GetMergedFreeBusy_ReturnsBadRequest_WhenToIsMissing` | ✅ |
| `GetMergedFreeBusy_ReturnsBadRequest_WhenWindowExceedsConfiguredMaximum` | ✅ |
| `GetMergedFreeBusy_ReturnsJsonWithStartAndEndFields` | ✅ |
| `GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId` | ✅ |
| `GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId` | ✅ |
| `GetMergedFreeBusy_ReturnsOk_WithValidParameters` | ✅ |
| `GetMergedFreeBusy_ReturnsUnauthorized_WithoutToken` | ✅ |
| `ListObfuscationProfiles_ReturnsDefaultProfiles_ForAuthenticatedCalendarOwner` | ✅ |
| `ListObfuscationProfiles_ReturnsNotFound_ForDifferentOwner` | ✅ |
| `SetObfuscationProfile_ResponseContainsAllFields` | ✅ |
| `SetObfuscationProfile_ReturnsBadRequest_ForUnknownContext` | ✅ |
| `SetObfuscationProfile_ReturnsBadRequest_ForZeroRoundingInterval` | ✅ |
| `SetObfuscationProfile_ReturnsNotFound_ForDifferentOwner` | ✅ |
| `SetObfuscationProfile_UpdatesClientProfile_ForAuthenticatedCalendarOwner` | ✅ |

</details>

<details>
<summary><b>EntraIdSysadminRoleTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `AdminEndpoints_AcceptSysadminRoleClaim_WithoutCalendarOwnerRecord` | ✅ |
| `HasSysadminRole_ReturnsFalse_ForDifferentRole` | ✅ |
| `HasSysadminRole_ReturnsTrue_ForClaimTypesRole` | ✅ |
| `HasSysadminRole_ReturnsTrue_ForEntraRolesClaim` | ✅ |
| `NonSysadminUser_CannotAccessAdminEndpoints` | ✅ |
| `UnknownUser_CannotAccessAdminEndpoints` | ✅ |

</details>

<details>
<summary><b>PeerConnectionsControllerTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `ListPeerConnections_ReturnsOnlyCurrentConsultantRequests` | ✅ |
| `RequestPeerConnection_AutoProvisionsCalendarOwner_WhenMissing` | ✅ |
| `RequestPeerConnection_CreatesRequestedRecord_ForAuthenticatedConsultant` | ✅ |
| `RequestPeerConnection_ReturnsConflict_WhenDuplicateForSameConsultant` | ✅ |
| `RequestPeerConnection_ReturnsUnauthorized_WhenUnauthenticated` | ✅ |
| `RequestPeerConnection_ReturnsValidationProblemDetails_WhenClientOrganisationNameMissing` | ✅ |

</details>

<details>
<summary><b>ShadowSlotsControllerTests</b> - 17/17</summary>

| Test | Result |
|------|--------|
| `PullBusySlotsForPeer_WithMissingPullScope_ReturnsForbidden` | ✅ |
| `PullBusySlotsForPeer_WithValidApiKeyAndMapping_ReturnsOk` | ✅ |
| `PullBusySlotsForPeer_WithValidApiKeyButNoMapping_ReturnsForbidden` | ✅ |
| `PullBusySlotsForPeer_WithoutApiKey_ReturnsUnauthorized` | ✅ |
| `PullBusySlots_WhenPeerExceedsSeparateRateLimit_ReturnsTooManyRequests` | ✅ |
| `PushShadowSlots_ReturnsBadRequest_WhenSlotBatchExceedsMaximum` | ✅ |
| `PushShadowSlots_ReturnsPayloadTooLarge_WhenRequestBodyExceedsConfiguredLimit` | ✅ |
| `PushShadowSlots_WhenPeerExceedsRateLimit_ReturnsTooManyRequestsAndKeepsOtherPeersUnthrottled` | ✅ |
| `PushShadowSlots_WithExpiredReplayTimestamp_ReturnsUnauthorized` | ✅ |
| `PushShadowSlots_WithInvalidApiKey_ReturnsUnauthorizedAndStoresNothing` | ✅ |
| `PushShadowSlots_WithMissingPushScope_ReturnsForbidden` | ✅ |
| `PushShadowSlots_WithOwnerScopedPayloadForUnmappedOwner_ReturnsForbidden` | ✅ |
| `PushShadowSlots_WithOwnerScopedPayload_StoresSlotsAndReturnsCreated` | ✅ |
| `PushShadowSlots_WithRevokedPeer_ReturnsUnauthorized` | ✅ |
| `PushShadowSlots_WithValidApiKeyButNoOwnerMappings_ReturnsForbidden` | ✅ |
| `PushShadowSlots_WithValidApiKey_StoresSlotsAndReturnsCreated` | ✅ |
| `PushShadowSlots_WithoutApiKeyHeader_ReturnsUnauthorized` | ✅ |

</details>

<details>
<summary><b>StatusControllerTests</b> - 8/8</summary>

| Test | Result |
|------|--------|
| `GetStatus_IncludesCalendarOwnerFields` | ✅ |
| `GetStatus_IncludesPeerConnectionStatus` | ✅ |
| `GetStatus_IncludesPeerLastSyncMetadata` | ✅ |
| `GetStatus_NullTimestamps_WhenNeverSynced` | ✅ |
| `GetStatus_ReturnsEmptyArray_WhenNoCalendarOwners` | ✅ |
| `GetStatus_ReturnsForbidden_WhenCallerLacksSysadminRole` | ✅ |
| `GetStatus_ReturnsOk_WhenCallerHasSysadminRole` | ✅ |
| `GetStatus_ReturnsUnauthorized_WhenNotAuthenticated` | ✅ |

</details>

<details>
<summary><b>SyncControllerTests</b> - 11/11</summary>

| Test | Result |
|------|--------|
| `GetPeerSyncStatus_ReturnsForbidden_WhenCallerLacksSysadminRole` | ✅ |
| `GetPeerSyncStatus_ReturnsOk_WhenCallerHasSysadminRole` | ✅ |
| `GetPeerSyncStatus_ReturnsUnauthorized_WhenNotAuthenticated` | ✅ |
| `PullBusySlotsForPeer_ReturnsBadRequest_WhenFromMissing` | ✅ |
| `PullBusySlotsForPeer_ReturnsBadRequest_WhenToMissing` | ✅ |
| `PushShadowSlots_CreatedResponse_ContainsLocationHeader` | ✅ |
| `PushShadowSlots_ReturnsBadRequest_WithInvalidPayload` | ✅ |
| `TriggerSync_ReturnsAccepted_WhenCallerHasSysadminRole` | ✅ |
| `TriggerSync_ReturnsAccepted_WhenTargetingSpecificOwner` | ✅ |
| `TriggerSync_ReturnsForbidden_WhenCallerLacksSysadminRole` | ✅ |
| `TriggerSync_ReturnsUnauthorized_WhenNotAuthenticated` | ✅ |

</details>

<details>
<summary><b>BrowserSsoTests</b> - 10/10</summary>

| Test | Result |
|------|--------|
| `CalendarOwnerDetail_DeniesAccessToDifferentOwner_ForNonSysadminUser` | ✅ |
| `CalendarOwnersPage_Loads_ForSysadminUser` | ✅ |
| `CalendarOwnersPage_ShowsUnauthorizedMessage_ForNonSysadminUser` | ✅ |
| `Dashboard_AutoProvisionsCalendarOwner_ForSignedInUser_WhenMissing` | ✅ |
| `Dashboard_IsScopedToSignedInUsersCalendarOwner_WhenNotSysadmin` | ✅ |
| `Dashboard_RendersLoginRedirect_ForAnonymousUser` | ✅ |
| `LoginEndpoint_RedirectsToEntraAuthorizeEndpoint` | ✅ |
| `PeerConnectionsPage_LoadsAdminView_ForSysadminUser` | ✅ |
| `PeerConnectionsPage_LoadsReadOnlyView_ForNonSysadminUser` | ✅ |
| `SwitchEndpoint_RedirectsToEntraAuthorizeEndpoint_WithSelectAccountPrompt` | ✅ |

</details>

<details>
<summary><b>InfrastructureDependencyInjectionTests</b> - 12/12</summary>

| Test | Result |
|------|--------|
| `CalendarOwnerCalendarSourceService_IsRegistered` | ✅ |
| `CalendarOwnerGraphConsentService_IsRegistered` | ✅ |
| `CalendarOwnerIcalFeedService_IsRegistered` | ✅ |
| `CalendarOwnerObfuscationProfileService_IsRegistered` | ✅ |
| `CalendarOwnerScopeResolver_IsRegistered` | ✅ |
| `CalendarSourceCatalog_ContainsAllThreeBuiltInPlugins` | ✅ |
| `CalendarSourceCatalog_IsRegistered` | ✅ |
| `CalendarSourceResolver_IsRegistered` | ✅ |
| `CalendarSource_IsRegistered` | ✅ |
| `InboundPeerPullSyncService_IsRegistered` | ✅ |
| `OutboundPeerSyncService_IsRegistered` | ✅ |
| `ShadowSlotStore_IsRegistered` | ✅ |

</details>

<details>
<summary><b>SecurityHardeningTests</b> - 2/2</summary>

| Test | Result |
|------|--------|
| `Production_HealthEndpoint_ReturnsExpectedSecurityHeaders` | ✅ |
| `Production_RegistersStrictCookiePolicyDefaults` | ✅ |

</details>

<details>
<summary><b>ShadowSlotStoreDependencyInjectionTests</b> - 1/1</summary>

| Test | Result |
|------|--------|
| `ShadowSlotStore_IsRegisteredAsSingleton` | ✅ |

</details>

<details>
<summary><b>SwaggerEndpointsTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `Development_OpenApiJson_ContainsOAuthSecurityDefinition` | ✅ |
| `Development_OpenApiJson_IsValidJson` | ✅ |
| `Development_SwaggerUi_ContainsConfiguredOAuthClientAndRedirectUri` | ✅ |
| `Development_SwaggerUi_IsAvailable` | ✅ |
| `Production_SwaggerEndpoints_AreNotAccessible` | ✅ |

</details>

<details>
<summary><b>EfCoreShadowSlotStoreTests</b> - 7/7</summary>

| Test | Result |
|------|--------|
| `GetAllSlotsAsync_ReturnsSlotsAcrossAllPeers` | ✅ |
| `GetAllSlotsAsync_WithOwnerScope_ExcludesSlotsWhenNoActivePeerRelationshipExists` | ✅ |
| `GetAllSlotsAsync_WithOwnerScope_ReturnsOnlyMatchingOwner` | ✅ |
| `GetSlotsAsync_ReturnsEmpty_WhenNothingStoredForPeer` | ✅ |
| `SetSlotsAsync_ReplacesExistingSlots_ForSamePeer` | ✅ |
| `SetSlotsAsync_ThenGetSlotsAsync_ReturnsSavedSlotsForPeer` | ✅ |
| `SetSlotsAsync_WithOwnerScope_ReplacesOnlyThatOwnerScope` | ✅ |

</details>

<details>
<summary><b>ShadowSlotRetentionTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `NewSlotsHaveCreatedAtUtcPopulated` | ✅ |
| `Purge_DeletesOldRows_ButPreservesRecentRows_ForSamePeer` | ✅ |
| `Purge_DeletesRowsOlderThanRetentionWindow` | ✅ |
| `Purge_PreservesRowsWithinRetentionWindow` | ✅ |
| `Purge_WithZeroRetentionDays_ShouldNotBeCalledByService` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerAvailabilitySyncServiceTests</b> - 8/8</summary>

| Test | Result |
|------|--------|
| `RunSyncCycleAsync_OnOwnerFailure_ContinuesWithNextOwnerAndRecordsFailureMetadata` | ✅ |
| `RunSyncCycleAsync_ProcessesAllCalendarOwnersAndStoresAvailabilitySnapshots` | ✅ |
| `RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeGoogleWriteBack` | ✅ |
| `RunSyncForOwnerAsync_WithWriteBackDisabled_DoesNotInvokeGraphWriteBack` | ✅ |
| `RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedGooglePlaceholder` | ✅ |
| `RunSyncForOwnerAsync_WithWriteBackEnabled_DeletesStaleManagedGraphPlaceholder` | ✅ |
| `RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedGooglePlaceholderUsingConfiguredTitle` | ✅ |
| `RunSyncForOwnerAsync_WithWriteBackEnabled_WritesManagedGraphPlaceholderUsingConfiguredTitle` | ✅ |

</details>

<details>
<summary><b>InboundPeerPullSyncServiceTests</b> - 10/10</summary>

| Test | Result |
|------|--------|
| `RunSyncCycleAsync_OnFailure_PreservesPreviouslyStoredSlots` | ✅ |
| `RunSyncCycleAsync_OnHttpFailure_RecordsLastSyncedAtAndNotSucceededOnPeerConnection` | ✅ |
| `RunSyncCycleAsync_OnSuccess_RecordsLastSyncedAtAndSucceededOnPeerConnection` | ✅ |
| `RunSyncCycleAsync_OnSuccess_ReplacesOwnerScopedSlotsAndSendsPeerHeaders` | ✅ |
| `RunSyncCycleAsync_SkipsWhenOnlyApiKeyIsConfigured` | ✅ |
| `RunSyncCycleAsync_SkipsWhenOnlyInstanceIdIsConfigured` | ✅ |
| `RunSyncCycleAsync_WhenPeerAFails_StillPullsFromPeerBAndLogsWarning` | ✅ |
| `RunSyncCycleAsync_WhenPeerIsUnreachable_LogsPeerIdAndFailureReason` | ✅ |
| `RunSyncCycleAsync_WithInvalidPeerBaseAddress_RecordsFailure` | ✅ |
| `RunSyncCycleAsync_WithZeroLookAheadDays_ClampsToOne` | ✅ |

</details>

<details>
<summary><b>OutboundPeerSyncServiceTests</b> - 8/8</summary>

| Test | Result |
|------|--------|
| `RunSyncCycleAsync_OnPeerHttpFailure_RecordsLastSyncedAtAndNotSucceededOnPeerConnection` | ✅ |
| `RunSyncCycleAsync_PostsCalendarOwnerRefAndBusySlotsWithPeerHeaders` | ✅ |
| `RunSyncCycleAsync_PropagatesPeerThumbprintsOnOutgoingRequest` | ✅ |
| `RunSyncCycleAsync_UsesOwnerClientObfuscationProfile` | ✅ |
| `RunSyncCycleAsync_WhenOnePeerFails_ContinuesWithRemainingPeersAndLogsWarning` | ✅ |
| `RunSyncCycleAsync_WithInvalidBaseAddress_RecordsFailureAndContinues` | ✅ |
| `RunSyncCycleAsync_WithNoMappings_DoesNotMakeHttpRequest` | ✅ |
| `RunSyncCycleAsync_WithZeroLookAheadDays_StillMakesRequest` | ✅ |

</details>

## Unit (406/406)

<details>
<summary><b>AggregateCalendarSourceTests</b> - 1/1</summary>

| Test | Result |
|------|--------|
| `WriteBackSlotsAsync_WritesOtherSourceBusySlotsToEachWritableDestination` | ✅ |

</details>

<details>
<summary><b>CalendarSourceContractTests</b> - 1/1</summary>

| Test | Result |
|------|--------|
| `GetEventsAsync_ReturnsOnlyEventsWithinRequestedWindow` | ✅ |

</details>

<details>
<summary><b>GoogleCalendarSourceCoreTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `GetEventsAsync_MapsGoogleResponse` | ✅ |
| `GetEventsAsync_RefreshesExpiredToken_BeforeGoogleCall` | ✅ |
| `GetEventsAsync_Throws_WhenGoogleApiBaseUrlIsMissing` | ✅ |
| `GetReadinessAsync_ReturnsNotReady_WhenNoCredentialsExist` | ✅ |
| `WriteBackSlotsAsync_CreatesPlaceholderEventsForInstance` | ✅ |
| `WriteBackSlotsAsync_ForCalendarOwner_CreatesPlaceholderEventsUsingEnabledGoogleInstance` | ✅ |

</details>

<details>
<summary><b>GraphCalendarSourceTests</b> - 17/17</summary>

| Test | Result |
|------|--------|
| `GetEventsAsync_FollowsNextLink_UntilExhausted` | ✅ |
| `GetEventsAsync_MapsGraphCalendarViewResponse` | ✅ |
| `GetEventsAsync_RefreshesExpiredToken_BeforeGraphCall` | ✅ |
| `GetEventsAsync_RetriesGraphRequest_WhenInitialResponseIsUnauthorized` | ✅ |
| `GetEventsAsync_ReturnsEmptyAndLogsWarning_WhenRefreshFails` | ✅ |
| `GetEventsAsync_ReusesRefreshedToken_ForNextLinkRequests` | ✅ |
| `GetEventsAsync_SkipsManagedPlaceholderEvents` | ✅ |
| `GetEventsAsync_StopsWhenNextLinkRepeats` | ✅ |
| `WriteBackSlotsAsync_CreatesPlaceholderEvents_ForEachActiveSlot` | ✅ |
| `WriteBackSlotsAsync_DeletesStaleEvents_WhenNoLongerActiveSlot` | ✅ |
| `WriteBackSlotsAsync_DoesNotDeleteManagedEvent_WhenStartIsOutsideWindow` | ✅ |
| `WriteBackSlotsAsync_ForSourceInstance_CreatesPlaceholderEvents` | ✅ |
| `WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenReadOnlyConsent` | ✅ |
| `WriteBackSlotsAsync_ForSourceInstance_SkipsWrite_WhenScopesAreNull` | ✅ |
| `WriteBackSlotsAsync_QueriesManagedEventsOnlyWithinWindow` | ✅ |
| `WriteBackSlotsAsync_SkipsWrite_WhenNoAccessToken` | ✅ |
| `WriteBackSlotsAsync_UsesCustomPlaceholderTitle` | ✅ |

</details>

<details>
<summary><b>ICloudCalendarSourceCoreTests</b> - 13/13</summary>

| Test | Result |
|------|--------|
| `CreateCalendarQueryRequest_ContainsExpandElement_ForRecurringEventSupport` | ✅ |
| `CreateCalendarQueryRequest_ProducesValidXml` | ✅ |
| `GetEventsAsync_AfterAutoMigration_StillReturnsEvents` | ✅ |
| `GetEventsAsync_WithFromAfterTo_ThrowsArgumentException` | ✅ |
| `GetEventsAsync_WithLegacyPlaintextInstanceSecrets_UsesFallbackAndReturnsEvents` | ✅ |
| `GetEventsAsync_WithMissingConfiguration_ReturnsEmpty` | ✅ |
| `GetEventsAsync_WithNullCalendarOwnerId_ReturnsEmpty` | ✅ |
| `GetEventsAsync_WithUnknownCalendarOwnerId_ReturnsEmpty` | ✅ |
| `GetReadinessAsync_WithLegacyPlaintextInstanceSecrets_AutoMigratesToProtectedSecretJson` | ✅ |
| `GetReadinessAsync_WithLegacyPlaintextInstanceSecrets_ReturnsReady` | ✅ |
| `GetReadinessAsync_WithNullContextSecretAndLegacyStoredJson_RecoversAndMigrates` | ✅ |
| `GetReadinessAsync_WithOwnerProtectedInstanceSecrets_AutoMigratesToInstanceSecretProtector` | ✅ |
| `GetReadinessAsync_WithProtectedBlobAndPlaintextContext_DoesNotRemigrate` | ✅ |

</details>

<details>
<summary><b>IcalFeedCalendarSourceTests</b> - 34/34</summary>

| Test | Result |
|------|--------|
| `GetEventsAsync_AggregatesEventsFromMultipleFeeds` | ✅ |
| `GetEventsAsync_AllDayEventWithSameDateDtstartDtend_NormalizesToOneDay` | ✅ |
| `GetEventsAsync_ContinuesWithOtherFeeds_WhenOneFeedFails` | ✅ |
| `GetEventsAsync_DoesNotThrow_WhenFromEqualsTo` | ✅ |
| `GetEventsAsync_ExcludesEventEndingExactlyAtFrom` | ✅ |
| `GetEventsAsync_ExcludesEventStartingExactlyAtTo` | ✅ |
| `GetEventsAsync_FallsBackToMockSource_WhenNoFeedsConfigured` | ✅ |
| `GetEventsAsync_ParsesAllDayEventWithValueDateAndExplicitDtend` | ✅ |
| `GetEventsAsync_ParsesAttendeeWithMailtoPrefix_StripsPrefix` | ✅ |
| `GetEventsAsync_ParsesAttendeeWithoutMailtoPrefix_KeepsOriginal` | ✅ |
| `GetEventsAsync_ParsesDateOnlyDtstart` | ✅ |
| `GetEventsAsync_ParsesDescription` | ✅ |
| `GetEventsAsync_ParsesDtstartWithTzidAmsterdam_ConvertsToUtc` | ✅ |
| `GetEventsAsync_ParsesDtstartWithUnknownTzid_FallsBackToFloatingUtc` | ✅ |
| `GetEventsAsync_ParsesDtstartWithoutZ_AssumedUtc` | ✅ |
| `GetEventsAsync_ParsesEventWithoutDescription_ReturnsNullDescription` | ✅ |
| `GetEventsAsync_ParsesEventWithoutSummary_DefaultsToBusy` | ✅ |
| `GetEventsAsync_ParsesEventWithoutUid_GeneratesId` | ✅ |
| `GetEventsAsync_ParsesLocation` | ✅ |
| `GetEventsAsync_ParsesPropertyWithSemicolonParameter` | ✅ |
| `GetEventsAsync_RejectsEventWithEndBeforeOrEqualStart` | ✅ |
| `GetEventsAsync_ReturnsEmptyForNonExistentOwner` | ✅ |
| `GetEventsAsync_ReturnsEmptyList_WhenAllFeedsFail` | ✅ |
| `GetEventsAsync_ReturnsEventsOrderedByStartAscending` | ✅ |
| `GetEventsAsync_ReturnsParsedEventsWithinRequestedWindow` | ✅ |
| `GetEventsAsync_SkipsLineWithColonAtPosition0` | ✅ |
| `GetEventsAsync_ThrowsArgumentException_WhenFromIsAfterTo` | ✅ |
| `GetEventsAsync_ThrowsOperationCanceled_WhenTokenCancelled` | ✅ |
| `GetEventsAsync_WithEmptyIcsContent_ReturnsEmpty` | ✅ |
| `GetEventsAsync_WithInvalidFeedUrl_ReturnsEmpty` | ✅ |
| `GetEventsAsync_WithLineFolding_ParsesContinuationLines` | ✅ |
| `GetEventsAsync_WithNullOwnerId_FallsBackToMockSource` | ✅ |
| `GetEventsAsync_WithTabLineFolding_ParsesContinuationLines` | ✅ |
| `GetEventsAsync_WithoutDtend_DateTimeStart_DefaultsTo30Minutes` | ✅ |

</details>

<details>
<summary><b>MockCalendarSourceTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `Application_ResolvesMockCalendarSource_AsActiveCalendarSource_InDevelopment` | ✅ |
| `GetEventsAsync_ReturnsAtLeastOneEventWithSensitiveFieldsPopulated` | ✅ |
| `GetEventsAsync_ReturnsOnlyEventsInsideRequestedWindow` | ✅ |
| `GetEventsAsync_ThrowsOperationCanceledException_WhenCancelled` | ✅ |
| `GetEventsAsync_WithFourteenDayWindowStartingToday_ReturnsAtLeastThreeEvents` | ✅ |

</details>

<details>
<summary><b>ConsentCallbackTests</b> - 21/21</summary>

| Test | Result |
|------|--------|
| `ConsentCallback_DisablesPrerender` | ✅ |
| `ConsentCallback_DoesNotDirectlyInjectConsentServices` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Authorization code is required to complete Google consent.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Calendar owner was not found.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Cannot access a disposed context instance. A common cause of this error is disposing a context instance that was resolved from dependency injection and then later trying to use the same context instance elsewhere in your application. This may occur if you are calling 'Dispose' on the context instance, or wrapping it in a using statement. If you are using dependency injection, you should let the dependency injection container take care of disposing context instances.
Object name: 'AppDbContext'.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Google calendar source instance was not found.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Google token exchange failed with HTTP 400: error='access_denied', description='User denied'.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Google token exchange failed with invalid_grant. The authorization code may be expired or already used, or the redirect URI did not exactly match the URI used during authorization.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("Graph calendar source instance was not found.")` | ✅ |
| `IsStateValidationFailure_ReturnsFalseForInfrastructureExceptions ("The specified calendar source instance is not a Google source.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state does not match this calendar source.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state has expired. Start consent again.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state is invalid or expired.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Google consent state is invalid.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state does not contain owner context. Use the instance-specific consent flow.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state has expired. Start consent again.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state is invalid or expired.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("Graph consent state is invalid.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("State is required to complete Google consent.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("State is required to complete Graph consent.")` | ✅ |
| `IsStateValidationFailure_ReturnsTrueForConsentStateMessages ("state is required to complete anything")` | ✅ |

</details>

<details>
<summary><b>ApplicationDependencyInjectionTests</b> - 8/8</summary>

| Test | Result |
|------|--------|
| `AddApplication_RegistersAllFiveEventTransformers` | ✅ |
| `AddApplication_RegistersMergeBlocksTransformer` | ✅ |
| `AddApplication_RegistersObfuscationPipeline` | ✅ |
| `AddApplication_RegistersRemoveAttendeesTransformer` | ✅ |
| `AddApplication_RegistersRemoveDescriptionTransformer` | ✅ |
| `AddApplication_RegistersRemoveLocationTransformer` | ✅ |
| `AddApplication_RegistersRemoveTitleTransformer` | ✅ |
| `AddApplication_RegistersRoundTimesTransformer` | ✅ |

</details>

<details>
<summary><b>ConfigurationDefaultsTests</b> - 4/4</summary>

| Test | Result |
|------|--------|
| `GraphConsentOptions_HasExpectedDefaults` | ✅ |
| `PeerConnection_ApiKeyHash_DefaultsToEmpty` | ✅ |
| `PeerTransportSecurityOptions_DefaultsToRejectingSelfSignedCertificates` | ✅ |
| `SyncOptions_HasExpectedDefaults` | ✅ |

</details>

<details>
<summary><b>DefaultLogRedactorTests</b> - 2/2</summary>

| Test | Result |
|------|--------|
| `Redact_MasksBearerTokenAndApiKey` | ✅ |
| `Redact_MasksConnectionStringPassword` | ✅ |

</details>

<details>
<summary><b>SecretStartupValidatorTests</b> - 2/2</summary>

| Test | Result |
|------|--------|
| `ValidateOrThrow_DoesNotThrow_WhenAllRequiredSecretsExist` | ✅ |
| `ValidateOrThrow_Throws_WhenRequiredSecretIsMissing` | ✅ |

</details>

<details>
<summary><b>ObfuscationPipelineLoggingTests</b> - 2/2</summary>

| Test | Result |
|------|--------|
| `Process_EmitsAuditLog_WithRequiredStructuredFields_AndNoSensitiveFields` | ✅ |
| `Process_WithEmptyEventList_StillEmitsAuditLog` | ✅ |

</details>

<details>
<summary><b>ObfuscationPipelineTests</b> - 25/25</summary>

| Test | Result |
|------|--------|
| `Process_KeepsSeparateSlots` | ✅ |
| `Process_MergesAdjacentSlots` | ✅ |
| `Process_MergesMultipleOverlappingSlots` | ✅ |
| `Process_MergesOverlappingSlots` | ✅ |
| `Process_MergesWithRoundingTransformer` | ✅ |
| `Process_TransformersAppliedInRegistrationOrder` | ✅ |
| `Process_WithCustomRoundingInterval_UsesProfileInterval` | ✅ |
| `Process_WithEmptyConsultantId_ThrowsArgumentException` | ✅ |
| `Process_WithEmptyEventList_ReturnsEmptyList` | ✅ |
| `Process_WithExplicitProfile_UsesProvidedProfile` | ✅ |
| `Process_WithFullPipeline_OutputContainsNoSensitiveFields` | ✅ |
| `Process_WithFullPipeline_PreservesSourceEventId` | ✅ |
| `Process_WithFullPipeline_ReturnsBusySlotsWithCorrectTimeWindow` | ✅ |
| `Process_WithIEnumerableEvents_StillWorks` | ✅ |
| `Process_WithMultipleEvents_ReturnsOneSlotPerEvent` | ✅ |
| `Process_WithNone_StillProducesBusySlotsWithCorrectWindow` | ✅ |
| `Process_WithNullConsultantId_ThrowsArgumentException` | ✅ |
| `Process_WithNullProfile_UsesDefaultProfile` | ✅ |
| `Process_WithProfileDisablingAttendees_KeepsAttendees` | ✅ |
| `Process_WithProfileDisablingDescription_KeepsDescription` | ✅ |
| `Process_WithProfileDisablingLocation_KeepsLocation` | ✅ |
| `Process_WithProfileDisablingMergeBlocks_KeepsSeparateSlots` | ✅ |
| `Process_WithProfileDisablingRoundTimes_KeepsOriginalTimes` | ✅ |
| `Process_WithProfileDisablingTitle_KeepsTitle` | ✅ |
| `Process_WithWhitespaceConsultantId_ThrowsArgumentException` | ✅ |

</details>

<details>
<summary><b>MergeBlocksTransformerTests</b> - 13/13</summary>

| Test | Result |
|------|--------|
| `Transform_PreservesObfuscatedDataInSourceSlots` | ✅ |
| `Transform_SortsUnsortedInputBeforeMerging` | ✅ |
| `Transform_ThreeSlots_FirstTwoMerge_ThirdSeparate` | ✅ |
| `Transform_WithAdjacentSlots_MergesIntoOne` | ✅ |
| `Transform_WithContainedSlot_PreservesOuterEnd` | ✅ |
| `Transform_WithEmptyList_ReturnsEmptyList` | ✅ |
| `Transform_WithIdenticalEndTimes_MergesCorrectly` | ✅ |
| `Transform_WithMergedSlots_CapturesAllSourceSlots` | ✅ |
| `Transform_WithNonOverlappingSlots_KeepsBoth` | ✅ |
| `Transform_WithOverlapping_WhereSecondEndsEarlier_KeepsFirstEnd` | ✅ |
| `Transform_WithOverlapping_WhereSecondEndsLater_TakesSecondEnd` | ✅ |
| `Transform_WithSingleSlot_ReturnsThatSlot` | ✅ |
| `Transform_WithThreeSlots_FirstTwoMerged_CapturesSourcesCorrectly` | ✅ |

</details>

<details>
<summary><b>RemoveAttendeesTransformerTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `RemoveAttendeesTransformer_ClearsAttendeeEmails` | ✅ |
| `RemoveAttendeesTransformer_PreservesDescription` | ✅ |
| `RemoveAttendeesTransformer_PreservesLocation` | ✅ |
| `RemoveAttendeesTransformer_PreservesTimeWindow` | ✅ |
| `RemoveAttendeesTransformer_PreservesTitle` | ✅ |

</details>

<details>
<summary><b>RemoveDescriptionTransformerTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `RemoveDescriptionTransformer_ClearsDescription` | ✅ |
| `RemoveDescriptionTransformer_DoesNotModifyAttendees` | ✅ |
| `RemoveDescriptionTransformer_DoesNotModifyLocation` | ✅ |
| `RemoveDescriptionTransformer_PreservesTimeWindow` | ✅ |
| `RemoveDescriptionTransformer_PreservesTitle` | ✅ |

</details>

<details>
<summary><b>RemoveLocationTransformerTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `RemoveLocationTransformer_ClearsLocation` | ✅ |
| `RemoveLocationTransformer_DoesNotModifyAttendees` | ✅ |
| `RemoveLocationTransformer_PreservesDescription` | ✅ |
| `RemoveLocationTransformer_PreservesTimeWindow` | ✅ |
| `RemoveLocationTransformer_PreservesTitle` | ✅ |

</details>

<details>
<summary><b>RemoveTitleTransformerTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `RemoveTitleTransformer_ClearsTitle` | ✅ |
| `RemoveTitleTransformer_DoesNotModifyAttendees` | ✅ |
| `RemoveTitleTransformer_DoesNotModifyLocation` | ✅ |
| `RemoveTitleTransformer_PreservesDescription` | ✅ |
| `RemoveTitleTransformer_PreservesTimeWindow` | ✅ |

</details>

<details>
<summary><b>RoundTimesTransformerTests</b> - 22/22</summary>

| Test | Result |
|------|--------|
| `RoundTimesTransformer_Arithmetic_WithOneMinuteInterval` | ✅ |
| `RoundTimesTransformer_EndExactlyAtMidnight_StaysAtMidnight` | ✅ |
| `RoundTimesTransformer_EndJustBeforeMidnight_RoundsUpToMidnight` | ✅ |
| `RoundTimesTransformer_EndJustPastMidnight_RoundsToNextBoundary` | ✅ |
| `RoundTimesTransformer_HandlesSpecialCase30Minutes` | ✅ |
| `RoundTimesTransformer_PreservesAttendees` | ✅ |
| `RoundTimesTransformer_PreservesDescription` | ✅ |
| `RoundTimesTransformer_PreservesId` | ✅ |
| `RoundTimesTransformer_PreservesLocation` | ✅ |
| `RoundTimesTransformer_PreservesTitle` | ✅ |
| `RoundTimesTransformer_RoundDown_JustPastBoundary` | ✅ |
| `RoundTimesTransformer_RoundUp_ExactlyOnBoundary_StaysSame` | ✅ |
| `RoundTimesTransformer_RoundUp_MidnightNonZeroRemainder` | ✅ |
| `RoundTimesTransformer_RoundsAlignedTimesUnchanged` | ✅ |
| `RoundTimesTransformer_RoundsEndTimeCrossingMidnight_ToStartOfNextDay` | ✅ |
| `RoundTimesTransformer_RoundsEndTimeUp` | ✅ |
| `RoundTimesTransformer_RoundsStartTimeDown` | ✅ |
| `RoundTimesTransformer_RoundsStartTo15MinuteBoundary` | ✅ |
| `RoundTimesTransformer_WithCustomInterval_RoundsCorrectly` | ✅ |
| `RoundTimesTransformer_WithNegativeInterval_ThrowsArgumentOutOfRange` | ✅ |
| `RoundTimesTransformer_WithPositiveInterval_DoesNotThrow` | ✅ |
| `RoundTimesTransformer_WithZeroInterval_ThrowsArgumentOutOfRange` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerGoogleConsentServiceTests</b> - 4/4</summary>

| Test | Result |
|------|--------|
| `BuildAuthorizationUrlAsync_IncludesSelectAccountPrompt` | ✅ |
| `BuildAuthorizationUrlAsync_UsesConfiguredRedirectUri_WhenPresent` | ✅ |
| `BuildAuthorizationUrlAsync_WithLocalDomainRedirectUri_ThrowsClearException` | ✅ |
| `CompleteConsentFromStateAsync_UsesRedirectUriStoredInState` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerGraphConsentServiceTests</b> - 27/27</summary>

| Test | Result |
|------|--------|
| `BuildAuthorizationUrlAsync_IncludesStatePrefixedWithGraph` | ✅ |
| `BuildAuthorizationUrl_ContainsResponseModeQuery` | ✅ |
| `BuildAuthorizationUrl_EmptyAuthorityTenant_FallsBackToAzureAdTenantId` | ✅ |
| `BuildAuthorizationUrl_FallsBackToAzureAdClientId_WhenConsentClientIdIsNull` | ✅ |
| `BuildAuthorizationUrl_IncludesPromptConsent` | ✅ |
| `BuildAuthorizationUrl_ReturnsValidUrl` | ✅ |
| `BuildAuthorizationUrl_TrimsTrailingSlashFromInstance` | ✅ |
| `BuildAuthorizationUrl_UsesConsentClientId` | ✅ |
| `BuildAuthorizationUrl_UsesCustomScope_WhenScopeIsConfigured` | ✅ |
| `BuildAuthorizationUrl_UsesDefaultScope_WhenScopeIsEmpty` | ✅ |
| `BuildAuthorizationUrl_WithRelativeUri_Throws` | ✅ |
| `CompleteConsentAsync_StoresTokensAndUpdatesTimestamps` | ✅ |
| `CompleteConsentAsync_ThenGetStatus_ShowsConsent` | ✅ |
| `CompleteConsentAsync_ThenHasConsent_ReturnsTrue` | ✅ |
| `CompleteConsentAsync_WithInvalidOwner_Throws` | ✅ |
| `CompleteConsentAsync_WithNullRefreshToken_StoresNull` | ✅ |
| `CompleteConsentAsync_WithWhitespaceRefreshToken_StoresNull` | ✅ |
| `CompleteConsentFromStateAsync_RoundTrip_CompletesConsent` | ✅ |
| `CompleteConsentFromStateAsync_WithInvalidToken_Throws` | ✅ |
| `CompleteConsentFromStateAsync_WithLegacyNonOwnerState_Throws` | ✅ |
| `CompleteConsentFromStateAsync_WithoutGraphPrefix_Throws` | ✅ |
| `GetStatusAsync_ReturnsNoConsent_ForNewOwner` | ✅ |
| `GetStatusAsync_ReturnsNull_WhenOwnerDoesNotExist` | ✅ |
| `GetStatusAsync_WithAccessTokenOnly_ReturnsHasConsent` | ✅ |
| `HasConsentAsync_ReturnsFalse_ForNewOwner` | ✅ |
| `HasConsentAsync_WithRefreshTokenOnly_ReturnsTrue` | ✅ |
| `HasConsentAsync_WithTokensExplicitlyCleared_ReturnsFalse` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerICloudConfigurationServiceTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `ClearConfigurationAsync_DisablesAndClearsInstancePayload` | ✅ |
| `GetConfigurationAsync_MasksStoredAppleId` | ✅ |
| `GetConfigurationAsync_WithUnconfiguredIcloudInstance_ReturnsConfiguredFalse` | ✅ |
| `GetConfigurationAsync_WithUnknownOwner_ReturnsNull` | ✅ |
| `SetConfigurationAsync_CreatesAndStoresIcloudInstanceConfiguration` | ✅ |
| `SetConfigurationAsync_WithUnknownOwner_ReturnsNull` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerIcalFeedServiceTests</b> - 14/14</summary>

| Test | Result |
|------|--------|
| `AddFeedAsync_ReturnsAdded_OnSuccess` | ✅ |
| `AddFeedAsync_ReturnsCalendarOwnerNotFound_WhenOwnerMissing` | ✅ |
| `AddFeedAsync_ReturnsDuplicate_WhenSameUrlAddedTwice` | ✅ |
| `AddFeedAsync_ReturnsInvalidUrl_WhenUrlValidatorRejectsInput` | ✅ |
| `AddFeedAsync_ReturnsNotFound_WhenDifferentOwnerIdProvided` | ✅ |
| `DeleteFeedAsync_DoesNotDeleteOtherOwnersFeed` | ✅ |
| `DeleteFeedAsync_RequiresBothFeedIdAndOwnerIdToMatch` | ✅ |
| `DeleteFeedAsync_ReturnsCalendarOwnerNotFound_WhenOwnerMissing` | ✅ |
| `DeleteFeedAsync_ReturnsDeleted_OnSuccess` | ✅ |
| `DeleteFeedAsync_ReturnsFeedNotFound_WhenFeedDoesNotExist` | ✅ |
| `DeleteFeedAsync_ReturnsNotFound_WhenDifferentOwnerIdProvided` | ✅ |
| `ListFeedsAsync_DoesNotReturnOtherOwnersFeeds` | ✅ |
| `ListFeedsAsync_ReturnsAddedFeeds` | ✅ |
| `ListFeedsAsync_ReturnsEmpty_WhenNoFeeds` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerObfuscationProfileServiceTests</b> - 16/16</summary>

| Test | Result |
|------|--------|
| `EnsureDefaultProfiles_CreatedWithSecureValues` | ✅ |
| `GetProfileAsync_ForExistingOwner_ReturnsSavedProfile` | ✅ |
| `GetProfileAsync_ForNonExistentOwner_ReturnsDefault` | ✅ |
| `GetProfileAsync_ReturnsDefaultForNewOwner` | ✅ |
| `GetProfileAsync_ReturnsDefault_WhenOwnerDoesNotExist` | ✅ |
| `GetProfilesAsync_AutoCreatesDefaultProfiles` | ✅ |
| `GetProfilesAsync_DoesNotDuplicate_OnRepeatedCalls` | ✅ |
| `GetProfilesAsync_ReturnedInCorrectOrder_NotDescending` | ✅ |
| `GetProfilesAsync_ReturnsSortedByContext` | ✅ |
| `SetProfileAsync_DoesNotAffectOtherContext` | ✅ |
| `SetProfileAsync_ForNonExistentOwner_DoesNotThrow` | ✅ |
| `SetProfileAsync_OnNewOwner_AutoCreatesAndUpdates` | ✅ |
| `SetProfileAsync_PersistsChanges` | ✅ |
| `SetProfileAsync_UpdatesExistingProfile` | ✅ |
| `SetProfileAsync_WithNegativeInterval_ThrowsArgumentOutOfRange` | ✅ |
| `SetProfileAsync_WithZeroInterval_ThrowsArgumentOutOfRange` | ✅ |

</details>

<details>
<summary><b>ObfuscationProfileDefaultsTests</b> - 4/4</summary>

| Test | Result |
|------|--------|
| `NewObfuscationProfile_HasSecureDefaults` | ✅ |
| `NewObfuscationProfile_PropertiesCanBeModified` | ✅ |
| `ObfuscationProfileSettings_CreateDefault_HasSecureDefaults` | ✅ |
| `ObfuscationProfileSettings_CreateDefault_InternalContext` | ✅ |

</details>

<details>
<summary><b>CalendarSourcePluginCatalogTests</b> - 10/10</summary>

| Test | Result |
|------|--------|
| `Discover_FindsAllThreeBuiltInPlugins` | ✅ |
| `Discover_IgnoresCalendarSourceTypes_WithoutAttribute` | ✅ |
| `Discover_MarksBuiltInPlugins_AsNotExternal` | ✅ |
| `Discover_ProvidesActionMetadata_ForPluginsWithConsentFlows` | ✅ |
| `Discover_ProvidesUiMetadata_ForBuiltInPlugins` | ✅ |
| `Discover_ReturnsDistinctPluginIds` | ✅ |
| `GetPlugin_FindsById_CaseInsensitively` | ✅ |
| `GetPlugin_ReturnsNull_ForNullOrWhitespace` | ✅ |
| `GetPlugin_ReturnsNull_ForUnknownId` | ✅ |
| `GetPlugins_ReturnsAllDescriptors_InAlphabeticalOrder` | ✅ |

</details>

<details>
<summary><b>CalendarSourceResolverTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `ResolveAsync_FallsBackToFirstPlugin_WhenConfiguredProviderNotInCatalog` | ✅ |
| `ResolveAsync_UsesConfiguredProvider_WhenNoOwnerIdProvided` | ✅ |
| `ResolveAsync_UsesConfiguredProvider_WhenOwnerHasNoSelection` | ✅ |
| `ResolveAsync_UsesConfiguredProvider_WhenOwnerIdNotInDatabase` | ✅ |
| `ResolveAsync_UsesOwnerPluginId_WhenOwnerHasExplicitSelection` | ✅ |

</details>

<details>
<summary><b>GoogleICloudPluginIntegrationTests</b> - 3/3</summary>

| Test | Result |
|------|--------|
| `GoogleAndICloudDlls_ShouldExistInPluginFolder` | ✅ |
| `GoogleICloudPlugins_ShouldBeDiscovered_WhenAssembliesAreLoaded` | ✅ |
| `PluginFolder_ShouldExist` | ✅ |

</details>

<details>
<summary><b>PluginAllowlistTests</b> - 14/14</summary>

| Test | Result |
|------|--------|
| `Cache_AfterInitialize_ContainsBlockedIds` | ✅ |
| `Cache_IsCaseInsensitive` | ✅ |
| `Cache_IsNotInitialized_BeforeInitializeIsCalled` | ✅ |
| `Cache_MarkAllowed_RemovesEntry` | ✅ |
| `Cache_MarkBlocked_AddsEntry` | ✅ |
| `Discover_AllowsBuiltInPlugins_WhenAllowlistDisabled` | ✅ |
| `Discover_AllowsBuiltInPlugins_WhenAllowlistEnabled` | ✅ |
| `Discover_DoesNotThrow_WhenAllowlistOptionsIsNull` | ✅ |
| `Discover_DoesNotThrow_WhenSomeAssemblyTypesCannotBeLoaded` | ✅ |
| `GetPlugin_ReturnsNull_ForBlockedPlugin` | ✅ |
| `GetPlugin_ReturnsPlugin_WhenNotBlocked` | ✅ |
| `GetPlugins_FiltersBlockedPlugins_ViaCache` | ✅ |
| `GetPlugins_ReflectsRuntimeToggle_ImmediatelyAfterCacheUpdate` | ✅ |
| `GetPlugins_ReturnsAll_WhenCacheIsNull` | ✅ |

</details>

<details>
<summary><b>PluginLoadingTests</b> - 1/1</summary>

| Test | Result |
|------|--------|
| `SimulatesStartup_LoadsAndDiscoversGoogleAndICloudPlugins` | ✅ |

</details>

<details>
<summary><b>AesGcmColumnEncryptorTests</b> - 7/7</summary>

| Test | Result |
|------|--------|
| `Constructor_ThrowsInvalidOperation_WhenKeyIsTooShort` | ✅ |
| `Constructor_ThrowsInvalidOperation_WhenSecretMissing` | ✅ |
| `Decrypt_RoundTrips_Correctly` | ✅ |
| `Decrypt_WithTamperedTag_ThrowsCryptographicException` | ✅ |
| `Encrypt_EmptyString_RoundTrips` | ✅ |
| `Encrypt_ProducesCiphertextDifferentFromPlaintext` | ✅ |
| `Encrypt_SamePlaintext_ProducesDifferentCiphertexts_PerCall` | ✅ |

</details>

<details>
<summary><b>PeerApiKeySecurityTests</b> - 3/3</summary>

| Test | Result |
|------|--------|
| `Hash_ProducesPbkdf2FormattedHash_AndVerifyPasses` | ✅ |
| `Verify_WithDifferentKey_Fails` | ✅ |
| `Verify_WithLegacySha256Hash_RemainsSupported` | ✅ |

</details>

<details>
<summary><b>PeerTransportSecurityTests</b> - 4/4</summary>

| Test | Result |
|------|--------|
| `NormalizeThumbprint_StripsWhitespaceAndSeparators` | ✅ |
| `ValidateRemoteCertificate_AcceptsPinnedCertificate_EvenWhenChainErrorsExist` | ✅ |
| `ValidateRemoteCertificate_AcceptsSelfSignedCertificate_WhenExplicitlyAllowed` | ✅ |
| `ValidateRemoteCertificate_RejectsMismatchedPinnedCertificate` | ✅ |

</details>

<details>
<summary><b>UrlSafetyValidatorTests</b> - 3/3</summary>

| Test | Result |
|------|--------|
| `ValidateAsync_ReturnsInvalid_ForHttpScheme` | ✅ |
| `ValidateAsync_ReturnsInvalid_ForPrivateIpAddress` | ✅ |
| `ValidateAsync_ReturnsValid_ForPublicHttpsUrl` | ✅ |

</details>

<details>
<summary><b>EfCoreShadowSlotStoreInMemoryTests</b> - 17/17</summary>

| Test | Result |
|------|--------|
| `GetAllSlotsAsync_ExcludesSlotEndingExactlyAtFrom` | ✅ |
| `GetAllSlotsAsync_FiltersByTimeWindow` | ✅ |
| `GetAllSlotsAsync_IncludesSlotAtExactFromBoundary` | ✅ |
| `GetAllSlotsAsync_IncludesSlotEndingAtToBoundary` | ✅ |
| `GetAllSlotsAsync_MapsAllFieldsFromUnscoped` | ✅ |
| `GetAllSlotsAsync_OnlyReturnsUnscoped_NotOwnerScoped` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_ExcludesSlotEndingExactlyAtFrom` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_ExcludesSlotStartingExactlyAtTo` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_FiltersByOwnerAndTimeWindow` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingWindow` | ✅ |
| `GetSlotsAsync_OwnerScoped_WithNullPeerId_Throws` | ✅ |
| `GetSlotsAsync_UnknownPeer_ReturnsEmpty` | ✅ |
| `SetAndGet_MapsAllFields` | ✅ |
| `SetSlotsAsync_OwnerScoped_ReplacesOnlyForThatOwner` | ✅ |
| `SetSlotsAsync_ReplacesExistingSlotsForSamePeer` | ✅ |
| `SetSlotsAsync_WithEmptyPeerId_Throws` | ✅ |
| `SetSlotsAsync_WithNullSlots_Throws` | ✅ |

</details>

<details>
<summary><b>EfCoreShadowSlotStoreLoggingTests</b> - 7/7</summary>

| Test | Result |
|------|--------|
| `GetAllSlotsAsync_EmitsLogWithSlotCount` | ✅ |
| `GetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount` | ✅ |
| `GetSlotsAsync_OwnerScoped_WithWhitespacePeerId_Throws` | ✅ |
| `GetSlotsAsync_WithWhitespacePeerId_Throws` | ✅ |
| `SetSlotsAsync_EmitsLogWithPeerIdAndCount` | ✅ |
| `SetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdOwnerIdAndCount` | ✅ |
| `SetSlotsAsync_OwnerScoped_WithWhitespacePeerId_Throws` | ✅ |

</details>

<details>
<summary><b>InMemoryShadowSlotStoreLoggingTests</b> - 5/5</summary>

| Test | Result |
|------|--------|
| `GetAllSlotsAsync_EmitsLogWithPeerCountAndSlotCount` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_EmitsLogWithOwnerIdAndSlotCount` | ✅ |
| `GetSlotsAsync_EmitsLogWithPeerIdAndCount` | ✅ |
| `SetAndGetSlots_EmitStructuredLogs_WithPeerIdAndCountOnly` | ✅ |
| `SetSlotsAsync_OwnerScoped_EmitsLogWithPeerIdAndOwnerIdAndCount` | ✅ |

</details>

<details>
<summary><b>InMemoryShadowSlotStoreTests</b> - 34/34</summary>

| Test | Result |
|------|--------|
| `GetAllSlotsAsync_ExcludesSlotEntirelyAfterWindow` | ✅ |
| `GetAllSlotsAsync_ExcludesSlotEntirelyBeforeWindow` | ✅ |
| `GetAllSlotsAsync_ExcludesSlotStartingExactlyAtTo` | ✅ |
| `GetAllSlotsAsync_IncludesSlotEndingAfterToButOverlapping` | ✅ |
| `GetAllSlotsAsync_IncludesSlotExactlyAtFromBoundary` | ✅ |
| `GetAllSlotsAsync_IncludesSlotExactlyAtToBoundary` | ✅ |
| `GetAllSlotsAsync_IncludesSlotStartingBeforeFromButOverlapping` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_AggregatesFromMultiplePeers` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_ExcludesSlotEntirelyAfterWindow` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_ExcludesSlotEntirelyOutsideWindow` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_FiltersToCorrectOwner` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_IncludesSlotFullyInsideWindow` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingEnd` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_IncludesSlotOverlappingStart` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_IncludesSlotSpanningEntireWindow` | ✅ |
| `GetAllSlotsAsync_OwnerScoped_WithCancelledToken_Throws` | ✅ |
| `GetAllSlotsAsync_ReturnsEmptyArray_WhenNoSlotsAreStored` | ✅ |
| `GetAllSlotsAsync_ReturnsSlotsAfterReplacingPeerSlots` | ✅ |
| `GetAllSlotsAsync_ReturnsSlotsFromMultiplePeers` | ✅ |
| `GetAllSlotsAsync_ReturnsSlotsFromSinglePeer` | ✅ |
| `GetAllSlotsAsync_WithCancelledToken_Throws` | ✅ |
| `GetSlotsAsync_OwnerScoped_UnknownPeerOwner_ReturnsEmptyList` | ✅ |
| `GetSlotsAsync_OwnerScoped_WithCancelledToken_Throws` | ✅ |
| `GetSlotsAsync_UnknownPeer_ReturnsEmptyList` | ✅ |
| `GetSlotsAsync_WithCancelledToken_Throws` | ✅ |
| `GetSlotsAsync_WithNullPeerId_Throws` | ✅ |
| `SetSlotsAsync_AndGetSlotsAsync_AreThreadSafeUnderConcurrentAccess` | ✅ |
| `SetSlotsAsync_ForDifferentPeers_KeepsDataIsolated` | ✅ |
| `SetSlotsAsync_OwnerScoped_WithCancelledToken_Throws` | ✅ |
| `SetSlotsAsync_OwnerScoped_WithNullPeerId_Throws` | ✅ |
| `SetSlotsAsync_OwnerScoped_WithNullSlots_Throws` | ✅ |
| `SetSlotsAsync_WithCancelledToken_Throws` | ✅ |
| `SetSlotsAsync_WithNullPeerId_Throws` | ✅ |
| `SetSlotsAsync_WithWhitespacePeerId_Throws` | ✅ |

</details>

<details>
<summary><b>CalendarOwnerAvailabilityBackgroundServiceTests</b> - 1/1</summary>

| Test | Result |
|------|--------|
| `StartAsync_InvokesCalendarOwnerAvailabilitySyncService` | ✅ |

</details>

<details>
<summary><b>PeerSyncBackgroundServiceTests</b> - 7/7</summary>

| Test | Result |
|------|--------|
| `ExecuteAsync_ContinuesAfterSyncFailure` | ✅ |
| `ExecuteAsync_RunsMultipleCycles_WithSmallInterval` | ✅ |
| `ExecuteAsync_UsesConfiguredInterval_ClampedToMinimum` | ✅ |
| `ExecuteAsync_WithLargeInterval_DoesNotRunSecondCycleQuickly` | ✅ |
| `ExecuteAsync_WithNegativeInterval_ClampsToOneSecond` | ✅ |
| `StartAsync_InvokesOutboundAndInboundSyncServices` | ✅ |
| `StopAsync_StopsGracefully` | ✅ |

</details>

<details>
<summary><b>GetBusySlotsQueryHandlerTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `Handle_MapsAllFieldsToResponse` | ✅ |
| `Handle_ReturnsObfuscatedBusySlots` | ✅ |
| `Handle_ThrowsRequestValidationException_WhenWindowExceedsConfiguredLimit` | ✅ |
| `Handle_UsesClientContext` | ✅ |
| `Handle_WithMultipleEvents_ReturnsOneResponsePerEvent` | ✅ |
| `Handle_WithNoEvents_ReturnsEmptyList` | ✅ |

</details>

<details>
<summary><b>GetMergedFreeBusyQueryHandlerTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `Handle_CombinesOwnAndShadowSlots_InSortedOrder` | ✅ |
| `Handle_PrefersPersistedAvailabilitySlots_WhenAvailable` | ✅ |
| `Handle_ReturnsOwnSlotsWhenNoShadowSlots` | ✅ |
| `Handle_ReturnsShadowSlotsWhenNoOwnEvents` | ✅ |
| `Handle_SortsCombinedSlots_ShadowBeforeOwn` | ✅ |
| `Handle_UsesInternalContext_ForObfuscation` | ✅ |

</details>

<details>
<summary><b>PushShadowSlotsCommandHandlerTests</b> - 6/6</summary>

| Test | Result |
|------|--------|
| `Handle_CreatesCorrectSourceEventId_WithIndex` | ✅ |
| `Handle_DeduplicatesOwnerIds` | ✅ |
| `Handle_MapsAllSlotFields` | ✅ |
| `Handle_StoresSlotsForEachDistinctOwner` | ✅ |
| `Handle_StoresSlotsWithPeerIdPrefix` | ✅ |
| `Handle_ThrowsRequestValidationException_WhenBatchExceedsLimit` | ✅ |

</details>

