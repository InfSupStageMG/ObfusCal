#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates a curated security test report for ObfusCal.

.DESCRIPTION
    Runs the security-focused automated tests, writes a TRX result file, and generates a
    Markdown summary that is suitable for security review. The report separates controls that
    are covered by automated tests from runtime-only checks that still require container or
    deployment verification.
#>
param(
    [switch]$NoOpen,
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$testResultsDir = Join-Path -Path (Join-Path -Path $repoRoot -ChildPath 'TestResults') -ChildPath 'Security'
$trxFile = Join-Path -Path $testResultsDir -ChildPath 'SecurityTests.trx'
$mdFile = Join-Path -Path $testResultsDir -ChildPath 'SecurityTests.md'

function Test-MatchesPattern {
    param(
        [string]$Value,
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Value -like "*$pattern*") {
            return $true
        }
    }

    return $false
}

$features = @(
    [pscustomobject]@{
        Name = '1c — Security response headers'
        Verification = 'Automated'
        RunPatterns = @('SecurityHeadersMiddlewareTests', 'SecurityHeadersIntegrationTests', 'Production_HealthEndpoint_ReturnsExpectedSecurityHeaders')
        MatchPatterns = @('SecurityHeadersMiddlewareTests', 'SecurityHeadersIntegrationTests', 'Production_HealthEndpoint_ReturnsExpectedSecurityHeaders')
        Notes = 'Headers are validated at middleware, integration, and production-pipeline levels.'
    },
    [pscustomobject]@{
        Name = '2a — HTTP peer base URL rejected'
        Verification = 'Automated'
        RunPatterns = @('Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesHttpScheme')
        MatchPatterns = @('Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesHttpScheme')
        Notes = 'The admin approve flow rejects unsafe http:// peer endpoints.'
    },
    [pscustomobject]@{
        Name = '2b — HTTPS enforced'
        Verification = 'Runtime'
        RunPatterns = @()
        MatchPatterns = @()
        Notes = 'Verify in the container/proxy deployment. TestServer does not reproduce Kestrel TLS redirection semantics.'
    },
    [pscustomobject]@{
        Name = '3a — HTTP iCal URL rejected'
        Verification = 'Automated'
        RunPatterns = @('AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesHttpScheme', 'ValidateAsync_ReturnsInvalid_ForHttpScheme')
        MatchPatterns = @('AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesHttpScheme', 'ValidateAsync_ReturnsInvalid_ForHttpScheme')
        Notes = 'Both controller-level and validator-level checks are covered.'
    },
    [pscustomobject]@{
        Name = '3b — Private IP blocked (SSRF)'
        Verification = 'Automated'
        RunPatterns = @('AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesPrivateIpHost', 'Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesPrivateIpHost', 'ValidateAsync_ReturnsInvalid_ForPrivateIpAddress')
        MatchPatterns = @('AddIcalFeed_ReturnsBadRequest_WhenFeedUrlUsesPrivateIpHost', 'Approve_ReturnsBadRequest_WhenPeerBaseUrlUsesPrivateIpHost', 'ValidateAsync_ReturnsInvalid_ForPrivateIpAddress')
        Notes = 'Controller endpoints and the shared URL validator both block private-network targets.'
    },
    [pscustomobject]@{
        Name = '4a — Cross-owner returns 404'
        Verification = 'Automated'
        RunPatterns = @(
            'GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId',
            'GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId',
            'GetBusySlots_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId',
            'GetMergedFreeBusy_ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId')
        MatchPatterns = @('ReturnsNotFound_WhenAuthenticatedCalendarOwnerRequestsDifferentId', 'ReturnsNotFound_WhenAuthenticatedCalendarOwnerIsAutoProvisionedButRequestsDifferentId')
        Notes = 'Both provisioned and auto-provisioned owners get a 404 when probing another owner.'
    },
    [pscustomobject]@{
        Name = '4b — Unauthenticated returns 401'
        Verification = 'Automated'
        RunPatterns = @('ReturnsUnauthorized_Without', 'ReturnsUnauthorized_WhenNotAuthenticated', 'PushShadowSlots_WithoutApiKeyHeader_ReturnsUnauthorized')
        MatchPatterns = @('ReturnsUnauthorized_Without', 'ReturnsUnauthorized_WhenNotAuthenticated', 'PushShadowSlots_WithoutApiKeyHeader_ReturnsUnauthorized')
        Notes = 'Anonymous access is rejected consistently for both owner and peer endpoints.'
    },
    [pscustomobject]@{
        Name = '4c/5 — Peer scope isolation'
        Verification = 'Automated'
        RunPatterns = @('WithOwnerScopedPayloadForUnmappedOwner_ReturnsForbidden', 'WithMissingPushScope_ReturnsForbidden', 'WithMissingPullScope_ReturnsForbidden')
        MatchPatterns = @('WithOwnerScopedPayloadForUnmappedOwner_ReturnsForbidden', 'WithMissingPushScope_ReturnsForbidden', 'WithMissingPullScope_ReturnsForbidden')
        Notes = 'Unmapped owners and missing scopes are both rejected with 403.'
    },
    [pscustomobject]@{
        Name = '6a — Invalid key → 401'
        Verification = 'Automated'
        RunPatterns = @('WithInvalidApiKey_ReturnsUnauthorizedAndStoresNothing')
        MatchPatterns = @('WithInvalidApiKey_ReturnsUnauthorizedAndStoresNothing')
        Notes = 'Bad peer credentials never write shadow slots.'
    },
    [pscustomobject]@{
        Name = '6b — Valid key + fresh timestamp'
        Verification = 'Automated'
        RunPatterns = @('WithValidApiKey_StoresSlotsAndReturnsCreated', 'WithValidApiKey_WritesSuccessAuditEvent')
        MatchPatterns = @('WithValidApiKey_StoresSlotsAndReturnsCreated', 'WithValidApiKey_WritesSuccessAuditEvent')
        Notes = 'A valid peer request succeeds and also emits an audit event.'
    },
    [pscustomobject]@{
        Name = '6c — Replay attack blocked'
        Verification = 'Automated'
        RunPatterns = @('WithExpiredReplayTimestamp_ReturnsUnauthorized')
        MatchPatterns = @('WithExpiredReplayTimestamp_ReturnsUnauthorized')
        Notes = 'Requests outside the allowed timestamp tolerance are rejected.'
    },
    [pscustomobject]@{
        Name = '6d — Revocation takes effect'
        Verification = 'Automated'
        RunPatterns = @('RevokeFlow_SetsRevokedAt_AndPeerAuthStopsImmediately', 'WithRevokedPeer_ReturnsUnauthorized')
        MatchPatterns = @('RevokeFlow_SetsRevokedAt_AndPeerAuthStopsImmediately', 'WithRevokedPeer_ReturnsUnauthorized')
        Notes = 'Revoked peers are rejected immediately in both admin and controller paths.'
    },
    [pscustomobject]@{
        Name = '6e — Key rotation'
        Verification = 'Automated'
        RunPatterns = @('RotateFlow_InvalidatesOldApiKeyAndActivatesNewApiKey', 'RotateFlow_WritesKeyRotationAuditEvent')
        MatchPatterns = @('RotateFlow_InvalidatesOldApiKeyAndActivatesNewApiKey', 'RotateFlow_WritesKeyRotationAuditEvent')
        Notes = 'Old credentials stop working and key rotation is audited.'
    },
    [pscustomobject]@{
        Name = '7a — Missing secret prevents startup'
        Verification = 'Automated'
        RunPatterns = @('SecretStartupValidatorTests')
        MatchPatterns = @('SecretStartupValidatorTests')
        Notes = 'Missing and whitespace-only secrets both fail startup validation.'
    },
    [pscustomobject]@{
        Name = '7b/7c — Credentials redacted'
        Verification = 'Automated'
        RunPatterns = @('DefaultLogRedactorTests', 'WriteAsync_Sanitizes', 'WriteAsync_WithoutRedactor_ExposesSensitiveValuesInAuditLog')
        MatchPatterns = @('DefaultLogRedactorTests', 'WriteAsync_Sanitizes', 'WriteAsync_WithoutRedactor_ExposesSensitiveValuesInAuditLog')
        Notes = 'Redaction is verified in both pure unit tests and audit-sink persistence tests.'
    },
    [pscustomobject]@{
        Name = '8a–8d — Rate limiting'
        Verification = 'Automated'
        RunPatterns = @(
            'PushShadowSlots_WhenPeerExceedsRateLimit_ReturnsTooManyRequestsAndKeepsOtherPeersUnthrottled',
            'PullBusySlots_WhenPeerExceedsSeparateRateLimit_ReturnsTooManyRequests',
            'PushShadowSlots_WhenUnauthenticatedRequestExceedsBackstopRateLimit_ReturnsTooManyRequests',
            'PushShadowSlots_ReturnsRetryAfterHeaderWithCorrectValue',
            'PushShadowSlots_DifferentEndpointHasSeparateRateLimit_PullDoesNotBlockPush')
        MatchPatterns = @('RateLimit_ReturnsTooManyRequests', 'ExceedsSeparateRateLimit_ReturnsTooManyRequests', 'ExceedsBackstopRateLimit_ReturnsTooManyRequests', 'ReturnsRetryAfterHeaderWithCorrectValue', 'SeparateRateLimit_PullDoesNotBlockPush')
        Notes = 'Per-peer, per-endpoint, and anonymous backstop quotas are all covered.'
    },
    [pscustomobject]@{
        Name = '9a — PBKDF2 key hashing'
        Verification = 'Automated'
        RunPatterns = @('PeerApiKeySecurityTests', 'ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash')
        MatchPatterns = @('PeerApiKeySecurityTests', 'ApproveFlow_ReturnsApiKeyOnce_AndStoresOnlySha256Hash')
        Notes = 'Hash format, verification, and storage behaviour are all exercised.'
    },
    [pscustomobject]@{
        Name = '10a–10e — Audit logging'
        Verification = 'Automated'
        RunPatterns = @('WritesAuthFailureAuditEventWithoutCredentialValue', 'WritesPeerSlotRejectedAuditEvent', 'WithValidApiKey_WritesSuccessAuditEvent', 'RotateFlow_WritesKeyRotationAuditEvent', 'WriteAsync_MultipleEvents_BuildsTamperEvidenceChain', 'WriteAsync_WithoutRedactor_ExposesSensitiveValuesInAuditLog')
        MatchPatterns = @('WritesAuthFailureAuditEventWithoutCredentialValue', 'WritesPeerSlotRejectedAuditEvent', 'WithValidApiKey_WritesSuccessAuditEvent', 'RotateFlow_WritesKeyRotationAuditEvent', 'WriteAsync_MultipleEvents_BuildsTamperEvidenceChain', 'WriteAsync_WithoutRedactor_ExposesSensitiveValuesInAuditLog')
        Notes = 'Audit creation, tamper-evidence chaining, and secret redaction are all verified.'
    }
)

$supportingRunPatterns = @(
    'AesGcmColumnEncryptorTests',
    'PeerTransportSecurityTests',
    'UrlSafetyValidatorTests',
    'SwaggerEndpointsTests',
    'Production_RegistersStrictCookiePolicyDefaults'
)

$allRunPatterns = New-Object System.Collections.Generic.List[string]
foreach ($feature in $features) {
    if ($feature.Verification -eq 'Automated') {
        foreach ($pattern in $feature.RunPatterns) {
            if (-not [string]::IsNullOrWhiteSpace($pattern)) {
                [void]$allRunPatterns.Add($pattern)
            }
        }
    }
}
foreach ($pattern in $supportingRunPatterns) {
    [void]$allRunPatterns.Add($pattern)
}
$allRunPatterns = $allRunPatterns | Sort-Object -Unique
$filterExpression = ($allRunPatterns | ForEach-Object { "FullyQualifiedName~$_" }) -join '|'

Write-Host '🔒 ObfusCal Security Test Report Generator' -ForegroundColor Cyan
Write-Host ''
New-Item -ItemType Directory -Force -Path $testResultsDir | Out-Null
if (Test-Path $trxFile) {
    Remove-Item $trxFile -Force
}

Write-Host '📋 Running curated security test suite...' -ForegroundColor Yellow
$dotnetTestArgs = @(
    'test',
    (Join-Path -Path $repoRoot -ChildPath 'ObfusCal.Tests\ObfusCal.Tests.csproj'),
    '--filter', $filterExpression,
    '--logger', 'trx;LogFileName=SecurityTests.trx',
    '--results-directory', $testResultsDir
)
if ($NoBuild) {
    $dotnetTestArgs += '--no-build'
}

$testOutput = & dotnet @dotnetTestArgs 2>&1
$dotnetExitCode = $LASTEXITCODE
$testOutput | Select-String -Pattern 'Passed!|Failed!|Test summary|Results File:' | ForEach-Object { Write-Host $_.ToString() }
Write-Host ''

if (-not (Test-Path $trxFile)) {
    Write-Error "Test results file not found: $trxFile"
    exit 1
}

Write-Host '📊 Parsing test results...' -ForegroundColor Yellow
[xml]$trx = Get-Content $trxFile
$definitionById = @{}
foreach ($unitTest in @($trx.TestRun.TestDefinitions.UnitTest)) {
    $className = [string]$unitTest.TestMethod.className
    $methodName = [string]$unitTest.TestMethod.name
    $fullyQualifiedName = if ([string]::IsNullOrWhiteSpace($className)) {
        $methodName
    }
    else {
        "$className.$methodName"
    }

    $definitionById[[string]$unitTest.id] = [pscustomobject]@{
        ClassName = $className
        MethodName = $methodName
        FullyQualifiedName = $fullyQualifiedName
    }
}

$results = New-Object System.Collections.Generic.List[object]
foreach ($result in @($trx.TestRun.Results.UnitTestResult)) {
    $definition = $definitionById[[string]$result.testId]
    $className = if ($null -ne $definition) { $definition.ClassName } else { '' }
    $methodName = if ($null -ne $definition) { $definition.MethodName } else { [string]$result.testName }
    $fullyQualifiedName = if ($null -ne $definition) { $definition.FullyQualifiedName } else { [string]$result.testName }

    [void]$results.Add([pscustomobject]@{
        TestId = [string]$result.testId
        Outcome = [string]$result.outcome
        Duration = if ($result.duration) { [string]$result.duration } else { '-' }
        TestName = [string]$result.testName
        ClassName = $className
        MethodName = $methodName
        FullyQualifiedName = $fullyQualifiedName
    })
}

$total = $results.Count
$passed = @($results | Where-Object { $_.Outcome -eq 'Passed' }).Count
$failed = @($results | Where-Object { $_.Outcome -eq 'Failed' }).Count
$skipped = @($results | Where-Object { $_.Outcome -eq 'NotExecuted' }).Count
$automatedFeatureCount = @($features | Where-Object { $_.Verification -eq 'Automated' }).Count
$runtimeFeatureCount = @($features | Where-Object { $_.Verification -eq 'Runtime' }).Count

$lines = New-Object System.Collections.Generic.List[string]
$lines.Add('# 🔒 ObfusCal Security Test Report')
$lines.Add('')
$lines.Add('## Executive Summary')
$lines.Add('')
$lines.Add('| Metric | Value |')
$lines.Add('|---|---|')
$lines.Add("| Generated | $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz') |")
$lines.Add("| Automated Security Tests | $total |")
$lines.Add("| Passed | ✅ $passed |")
$lines.Add("| Failed | ❌ $failed |")
$lines.Add("| Skipped | ⏭ $skipped |")
$lines.Add("| Automated Feature Rows | $automatedFeatureCount |")
$lines.Add("| Runtime-only Feature Rows | $runtimeFeatureCount |")
$passRate = if ($total -gt 0) { '{0:P0}' -f ($passed / $total) } else { 'N/A' }
$lines.Add("| Pass Rate | $passRate |")
$lines.Add('')
$lines.Add('**Source:** `TestResults/Security/SecurityTests.trx`')
$lines.Add('')
$lines.Add('**Documentation:** See [SECURITY_TESTING.md](../../SECURITY_TESTING.md) for the full validation catalogue and runtime-only checks.')
$lines.Add('')

$lines.Add('## Security Feature Coverage')
$lines.Add('')
$lines.Add('| Feature | Verification | Coverage | Status | Notes |')
$lines.Add('|---|---|---|---|---|')

$mappedTestIds = New-Object System.Collections.Generic.HashSet[string]
foreach ($feature in $features) {
    if ($feature.Verification -ne 'Automated') {
        $lines.Add("| $($feature.Name) | Runtime/manual | n/a | ℹ️ Runtime-only | $($feature.Notes) |")
        continue
    }

    $matchedTests = New-Object System.Collections.Generic.List[object]
    foreach ($result in $results) {
        if (Test-MatchesPattern -Value $result.FullyQualifiedName -Patterns $feature.MatchPatterns) {
            [void]$matchedTests.Add($result)
            [void]$mappedTestIds.Add($result.TestId)
        }
    }

    $matchedTests = @($matchedTests | Sort-Object TestId -Unique)
    $featurePassed = @($matchedTests | Where-Object { $_.Outcome -eq 'Passed' }).Count
    $featureTotal = $matchedTests.Count

    $status = if ($featureTotal -eq 0) { '⚠️ No automated tests matched' }
    elseif ($featurePassed -eq $featureTotal) { '✅ All pass' }
    else { "❌ $featurePassed/$featureTotal pass" }

    $coverage = if ($featureTotal -gt 0) { "$featurePassed/$featureTotal" } else { '0/0' }
    $lines.Add("| $($feature.Name) | Automated | $coverage | $status | $($feature.Notes) |")
}

$lines.Add('')
$lines.Add('## Detailed Automated Test Results')
$lines.Add('')
$lines.Add('| Outcome | Test | Duration |')
$lines.Add('|---|---|---|')
foreach ($result in ($results | Sort-Object FullyQualifiedName)) {
    $outcomeEmoji = if ($result.Outcome -eq 'Passed') { '✅' } elseif ($result.Outcome -eq 'Failed') { '❌' } else { '⏭' }
    $displayName = if ([string]::IsNullOrWhiteSpace($result.ClassName)) { $result.TestName } else { "$($result.ClassName).$($result.MethodName)" }
    $lines.Add('| ' + $outcomeEmoji + ' | `' + $displayName + '` | ' + $result.Duration + ' |')
}

$supportingResults = @($results | Where-Object { -not $mappedTestIds.Contains($_.TestId) } | Sort-Object FullyQualifiedName)
if ($supportingResults.Count -gt 0) {
    $lines.Add('')
    $lines.Add('## Supporting Security Regression Tests')
    $lines.Add('')
    $lines.Add('These tests are part of the curated security suite but are not mapped to a single row in the feature table.')
    $lines.Add('')
    foreach ($result in $supportingResults) {
        $displayName = if ([string]::IsNullOrWhiteSpace($result.ClassName)) { $result.TestName } else { "$($result.ClassName).$($result.MethodName)" }
        $lines.Add('- `' + $displayName + '`')
    }
}

$lines.Add('')
$lines.Add('## Reproduce These Results')
$lines.Add('')
$lines.Add('Generate the same report:')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add('.\TestResults\Run-SecurityTestReport.ps1')
$lines.Add('```')
$lines.Add('')
$lines.Add('Generate the report without rebuilding first:')
$lines.Add('')
$lines.Add('```powershell')
$lines.Add('.\TestResults\Run-SecurityTestReport.ps1 -NoBuild')
$lines.Add('```')
$lines.Add('')
$lines.Add('## Presentation Note')
$lines.Add('')
$lines.Add('- **Automated** rows are backed by executable tests in this report.')
$lines.Add('- **Runtime/manual** rows require deployment-level evidence such as container inspection, proxy behaviour, or direct environment verification.')
$lines.Add('- For security review, present this report together with `SECURITY_TESTING.md` so auditors can see both the automated evidence and the residual runtime checks.')

Set-Content -Path $mdFile -Value $lines -Encoding UTF8

Write-Host ''
Write-Host '✅ Report generated:' -ForegroundColor Green
Write-Host "   Markdown: $mdFile"
Write-Host "   TRX:      $trxFile"
Write-Host ''

if (-not $NoOpen) {
    try {
        Invoke-Item $mdFile
    }
    catch {
        Write-Host "ℹ️  Report ready at: $mdFile" -ForegroundColor Cyan
    }
}

if ($dotnetExitCode -ne 0 -and $failed -eq 0) {
    exit $dotnetExitCode
}

$exitCode = if ($failed -eq 0) { 0 } else { 1 }
exit $exitCode

