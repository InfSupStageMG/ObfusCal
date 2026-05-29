using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Components.Pages;

/// <summary>
/// Displays peer sync status and allows admins to trigger an on-demand sync cycle.
/// </summary>
public partial class SyncStatus : ComponentBase, IDisposable
{
    [Inject] private IPeerConnectionService PeerConnectionService { get; set; } = default!;
    [Inject] private IOutboundPeerSyncService OutboundSyncService { get; set; } = default!;
    [Inject] private IInboundPeerPullSyncService InboundSyncService { get; set; } = default!;
    [Inject] private ISyncProgressMonitor SyncProgressMonitor { get; set; } = default!;
    [Inject] private ISyncRuntimeOptionsProvider SyncRuntimeOptionsProvider { get; set; } = default!;
    [Inject] private IPeerSyncHistoryStore PeerSyncHistoryStore { get; set; } = default!;

    private List<PeerSyncStatus>? _peers;
    private bool _loading = true;
    private bool _syncing;
    private string? _syncMessage;
    private MessageIntent _syncMessageIntent = MessageIntent.Success;
    private SyncProgressUpdate? _syncProgress;
    private Timer? _countdownTimer;
    private DateTimeOffset? _lastDisplayedSyncCompletedAt;

    private DateTimeOffset? LastPeerSyncCompletedAt
    {
        get
        {
            var monitorValue = SyncProgressMonitor.LastPeerSyncCompletedAt;
            if (_lastDisplayedSyncCompletedAt is null)
            {
                return monitorValue;
            }

            if (monitorValue is null)
            {
                return _lastDisplayedSyncCompletedAt;
            }

            return monitorValue.Value >= _lastDisplayedSyncCompletedAt.Value
                ? monitorValue
                : _lastDisplayedSyncCompletedAt;
        }
    }

    private string NextSyncCountdownText
    {
        get
        {
            var intervalSeconds = Math.Max(1, SyncRuntimeOptionsProvider.Get().SyncIntervalSeconds);
            var intervalText = FormatInterval(intervalSeconds);
            if (LastPeerSyncCompletedAt is null)
            {
                return $"No background sync has run yet this session (interval: every {intervalText}).";
            }

            var nextAt = LastPeerSyncCompletedAt.Value.AddSeconds(intervalSeconds);
            var remaining = nextAt - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                return $"Next sync in {FormatRemaining(remaining)} (every {intervalText}).";
            }

            return $"Next sync is imminent (every {intervalText}).";
        }
    }

    private string? LastSyncCompletedText => LastPeerSyncCompletedAt is null
        ? null
        : $"Last sync completed at {LastPeerSyncCompletedAt.Value:HH:mm:ss} UTC.";

    protected override async Task OnInitializedAsync()
    {
        _lastDisplayedSyncCompletedAt = await PeerSyncHistoryStore.GetLastCompletedAtUtcAsync();
        await LoadPeersAsync();
        _countdownTimer = new Timer(
            state => _ = InvokeAsync(StateHasChanged),
            null,
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1));
    }

    private async Task LoadPeersAsync()
    {
        _loading = true;
        try
        {
            _peers = (await PeerConnectionService.ListSyncStatusAsync()).ToList();
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task TriggerSyncAsync()
    {
        if (SyncProgressMonitor.IsPeerSyncInProgress)
        {
            return;
        }

        _syncing = true;
        _syncMessage = null;
        _syncProgress = null;
        StateHasChanged();

        var progress = new Progress<SyncProgressUpdate>(update =>
        {
            _syncProgress = update;
            _ = InvokeAsync(StateHasChanged);
        });

        try
        {
            await OutboundSyncService.RunSyncCycleAsync(CancellationToken.None, progress);
            await InboundSyncService.RunSyncCycleAsync(CancellationToken.None, progress);
            _lastDisplayedSyncCompletedAt = DateTimeOffset.UtcNow;
            await PeerSyncHistoryStore.SetLastCompletedAtUtcAsync(_lastDisplayedSyncCompletedAt.Value);
            _syncMessage = $"Sync completed at {_lastDisplayedSyncCompletedAt.Value:HH:mm:ss} UTC.";
            _syncMessageIntent = MessageIntent.Success;
            StateHasChanged();
        }
        catch (OperationCanceledException)
        {
            _syncMessage = "Sync was canceled.";
            _syncMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _syncing = false;
            _syncProgress = null;
            await LoadPeersAsync();
        }
    }

    private static string FormatRemaining(TimeSpan remaining)
    {
        var totalSeconds = (int)Math.Ceiling(remaining.TotalSeconds);
        if (totalSeconds < 0)
        {
            totalSeconds = 0;
        }

        var minutes = totalSeconds / 60;
        var seconds = totalSeconds % 60;
        return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
    }

    private static string FormatInterval(int intervalSeconds)
    {
        if (intervalSeconds % 60 == 0)
        {
            var minutes = intervalSeconds / 60;
            return minutes == 1 ? "1 min" : $"{minutes} min";
        }

        return intervalSeconds == 1 ? "1 sec" : $"{intervalSeconds} sec";
    }

    public void Dispose()
    {
        _countdownTimer?.Dispose();
    }
}






