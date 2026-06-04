using Microsoft.FluentUI.AspNetCore.Components;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    // Write-back state - initialised from _owner in OnInitializedAsync
    private bool _writeBackEnabled;
    private string? _writeBackPlaceholderTitle;
    private bool _savingWriteBack;
    private string? _writeBackMessage;
    private MessageIntent _writeBackMessageIntent = MessageIntent.Info;

    private SyncProgressUpdate? _ownerSyncProgress;

    private async Task TriggerOwnerSyncAsync()
    {
        _triggeringSyncForOwner = true;
        _ownerSyncMessage = null;
        _ownerSyncProgress = null;
        StateHasChanged();

        var progress = new Progress<SyncProgressUpdate>(update =>
        {
            _ownerSyncProgress = update;
            InvokeAsync(StateHasChanged);
        });

        try
        {
            await AvailabilitySyncService.RunSyncForOwnerAsync(Id, CancellationToken.None, progress);
            _ownerSyncMessage = $"Sync completed at {DateTimeOffset.UtcNow:HH:mm:ss} UTC.";
            _ownerSyncMessageIntent = MessageIntent.Success;
        }
        catch (InvalidOperationException ex)
        {
            _ownerSyncMessage = $"Sync failed: {ex.Message}";
            _ownerSyncMessageIntent = MessageIntent.Error;
        }
        catch (HttpRequestException ex)
        {
            _ownerSyncMessage = $"Sync failed: {ex.Message}";
            _ownerSyncMessageIntent = MessageIntent.Error;
        }
        catch (TaskCanceledException ex)
        {
            _ownerSyncMessage = $"Sync failed: {ex.Message}";
            _ownerSyncMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _triggeringSyncForOwner = false;
            _ownerSyncProgress = null;
        }
    }

    private async Task SaveWriteBackSettingsAsync()
    {
        _savingWriteBack = true;
        _writeBackMessage = null;
        StateHasChanged();

        try
        {
            await CalendarOwnerService.UpdateWriteBackSettingsAsync(
                Id,
                _writeBackEnabled,
                _writeBackPlaceholderTitle);
            _writeBackMessage = "Write-back settings saved.";
            _writeBackMessageIntent = MessageIntent.Success;
        }
        catch (InvalidOperationException ex)
        {
            _writeBackMessage = $"Failed to save settings: {ex.Message}";
            _writeBackMessageIntent = MessageIntent.Error;
        }
        catch (HttpRequestException ex)
        {
            _writeBackMessage = $"Failed to save settings: {ex.Message}";
            _writeBackMessageIntent = MessageIntent.Error;
        }
        catch (TaskCanceledException ex)
        {
            _writeBackMessage = $"Failed to save settings: {ex.Message}";
            _writeBackMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _savingWriteBack = false;
        }
    }
}

