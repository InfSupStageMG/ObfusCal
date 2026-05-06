using Microsoft.FluentUI.AspNetCore.Components;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
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
}

