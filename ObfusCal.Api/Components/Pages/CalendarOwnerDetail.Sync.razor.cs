using Microsoft.FluentUI.AspNetCore.Components;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    // Write-back state - initialised from _owner in OnInitializedAsync
    private bool _writeBackEnabled;
    private string? _writeBackPlaceholderTitle;
    private bool _savingWriteBack;
    private string? _writeBackMessage;
    private MessageIntent _writeBackMessageIntent = MessageIntent.Info;

    private async Task TriggerOwnerSyncAsync()
    {
        _triggeringSyncForOwner = true;
        _ownerSyncMessage = null;
        StateHasChanged();

        try
        {
            await AvailabilitySyncService.RunSyncForOwnerAsync(Id);
            _ownerSyncMessage = $"Sync completed at {DateTimeOffset.UtcNow:HH:mm:ss} UTC.";
            _ownerSyncMessageIntent = MessageIntent.Success;
        }
        catch (Exception ex)
        {
            _ownerSyncMessage = $"Sync failed: {ex.Message}";
            _ownerSyncMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _triggeringSyncForOwner = false;
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
        catch (Exception ex)
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

