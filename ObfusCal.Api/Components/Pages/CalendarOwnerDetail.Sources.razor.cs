using Microsoft.FluentUI.AspNetCore.Components;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    private void LoadPluginCatalog()
    {
        _pluginOptions.Clear();
        foreach (var plugin in CalendarSourceCatalog.GetPlugins())
        {
            var ui = plugin.Ui;
            _pluginOptions.Add(new PluginOption(
                plugin.Id,
                plugin.DisplayName,
                plugin.IsExternalPlugin,
                ui?.SupportsMultipleInstances ?? true,
                ui?.ConfigurationJsonTemplate,
                ui?.SecretDataJsonTemplate,
                ui?.SetupHint));
        }

        _selectedPluginOption = _pluginOptions.FirstOrDefault();
        ApplyPluginDefaults();
    }

    private async Task LoadSourceInstancesAsync()
    {
        _sourceInstances.Clear();
        var instances = await CalendarSourceInstanceService.ListAsync(Id);
        foreach (var instance in instances)
        {
            var plugin = _pluginOptions.FirstOrDefault(option =>
                string.Equals(option.Id, instance.PluginId, StringComparison.OrdinalIgnoreCase));

            var actions = CalendarSourceCatalog.GetPlugin(instance.PluginId)?.Ui?.Actions ?? [];
            var configurationFields =
                BuildFieldEditorsFromTemplate(plugin?.ConfigurationJsonTemplate, instance.ConfigurationJson);
            var secretFields = BuildFieldEditorsFromTemplate(plugin?.SecretDataJsonTemplate);

            _sourceInstances.Add(new SourceInstanceEditor
            {
                Id = instance.Id,
                PluginId = instance.PluginId,
                PluginDisplayName = plugin?.DisplayName ?? instance.PluginId,
                DisplayName = instance.DisplayName,
                IsEnabled = instance.IsEnabled,
                IsReady = instance.IsReady,
                Title = instance.Title,
                Detail = instance.Detail,
                ConfigurationJson = instance.ConfigurationJson,
                SecretDataJson = string.Empty,
                ConfigurationFields = configurationFields,
                SecretFields = secretFields,
                Actions = actions
            });
        }

        _canConfigureGraphWriteBack = _sourceInstances.Any(instance =>
            instance.IsEnabled && string.Equals(instance.PluginId, "graph", StringComparison.OrdinalIgnoreCase));
    }

    private void ApplyPluginDefaults()
    {
        if (_selectedPluginOption is null)
        {
            _newSourceDisplayName = null;
            _newSourceConfigurationJson = null;
            _newSourceSecretDataJson = null;
            _newSourceConfigurationFields = [];
            _newSourceSecretFields = [];
            return;
        }

        _newSourceDisplayName = null;
        _newSourceConfigurationJson = _selectedPluginOption.ConfigurationJsonTemplate;
        _newSourceSecretDataJson = _selectedPluginOption.SecretDataJsonTemplate;
        _newSourceConfigurationFields = BuildFieldEditorsFromTemplate(_selectedPluginOption.ConfigurationJsonTemplate);
        _newSourceSecretFields = BuildFieldEditorsFromTemplate(_selectedPluginOption.SecretDataJsonTemplate);
        _newSourceIsEnabled = true;
    }

    private async Task CreateSourceInstanceAsync()
    {
        _sourceMessage = null;
        _lastActionInstanceId = null;
        if (_selectedPluginOption is null)
            return;

        if (!_selectedPluginOption.SupportsMultipleInstances
            && _sourceInstances.Any(instance =>
                string.Equals(instance.PluginId, _selectedPluginOption.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _sourceMessage = $"{_selectedPluginOption.DisplayName} supports only one source instance.";
            _sourceMessageIntent = MessageIntent.Warning;
            return;
        }

        var configurationJson = HasFieldEditors(_newSourceConfigurationFields)
            ? SerializeFieldEditors(_newSourceConfigurationFields)
            : NormalizeJsonInput(_newSourceConfigurationJson);
        var secretDataJson = HasFieldEditors(_newSourceSecretFields)
            ? SerializeFieldEditors(_newSourceSecretFields)
            : NormalizeJsonInput(_newSourceSecretDataJson);

        _creatingSourceInstance = true;
        try
        {
            var created = await CalendarSourceInstanceService.CreateAsync(
                Id,
                new CreateCalendarSourceInstanceInput(
                    _selectedPluginOption.Id,
                    string.IsNullOrWhiteSpace(_newSourceDisplayName)
                        ? _selectedPluginOption.DisplayName
                        : _newSourceDisplayName,
                    configurationJson,
                    secretDataJson,
                    _newSourceIsEnabled));

            if (created is null)
            {
                _sourceMessage = "Unable to create source instance.";
                _sourceMessageIntent = MessageIntent.Error;
                return;
            }

            _sourceMessage = $"Added source instance '{created.DisplayName}'. Triggering sync...";
            _sourceMessageIntent = MessageIntent.Success;
            _showAddForm = false;

            // Reload the source list, but do not let a readiness-check failure (e.g. a
            // plugin's CalDAV/OAuth probe throwing) block the sync trigger below.
            try { await LoadSourceInstancesAsync(); }
            catch { /* stale list is acceptable; the snapshot sync still fires */ }
            ApplyPluginDefaults();

            await TryRunAvailabilitySyncAsync(
                $"Added source instance '{created.DisplayName}' and synced availability.",
                $"Added source instance '{created.DisplayName}', but sync failed");
        }
        catch (Exception ex)
        {
            _sourceMessage = ex.Message;
            _sourceMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _creatingSourceInstance = false;
        }
    }

    private async Task UpdateSourceInstanceAsync(SourceInstanceEditor instance)
    {
        _sourceMessage = null;
        _lastActionInstanceId = instance.Id;
        _updatingSourceInstanceId = instance.Id;
        try
        {
            var configurationJson = HasFieldEditors(instance.ConfigurationFields)
                ? SerializeFieldEditors(instance.ConfigurationFields)
                : NormalizeJsonInput(instance.ConfigurationJson);
            var secretDataJson = HasFieldEditors(instance.SecretFields)
                ? SerializeFieldEditors(instance.SecretFields)
                : NormalizeJsonInput(instance.SecretDataJson);

            var updated = await CalendarSourceInstanceService.UpdateAsync(
                Id,
                instance.Id,
                new UpdateCalendarSourceInstanceInput(
                    string.IsNullOrWhiteSpace(instance.DisplayName) ? null : instance.DisplayName,
                    configurationJson,
                    secretDataJson,
                    instance.IsEnabled));

            if (updated is null)
            {
                _sourceMessage = "Source instance not found.";
                _sourceMessageIntent = MessageIntent.Warning;
                return;
            }

            _sourceMessage = $"Updated source instance '{updated.DisplayName}'. Triggering sync...";
            _sourceMessageIntent = MessageIntent.Success;

            // Same guard as CreateSourceInstanceAsync: readiness-check failures in the
            // list reload must not prevent the sync trigger.
            try { await LoadSourceInstancesAsync(); }
            catch { /* stale list is acceptable; the snapshot sync still fires */ }

            await TryRunAvailabilitySyncAsync(
                $"Updated source instance '{updated.DisplayName}' and synced availability.",
                $"Updated source instance '{updated.DisplayName}', but sync failed");
        }
        catch (Exception ex)
        {
            _sourceMessage = ex.Message;
            _sourceMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _updatingSourceInstanceId = null;
        }
    }

    private async Task DeleteSourceInstanceAsync(Guid sourceInstanceId)
    {
        _sourceMessage = null;
        _lastActionInstanceId = sourceInstanceId;
        _deletingSourceInstanceId = sourceInstanceId;
        try
        {
            var deleted = await CalendarSourceInstanceService.DeleteAsync(Id, sourceInstanceId);
            if (!deleted)
            {
                _sourceMessage = "Source instance was not found.";
                _sourceMessageIntent = MessageIntent.Warning;
                return;
            }

            _sourceMessage = "Source instance deleted.";
            _sourceMessageIntent = MessageIntent.Success;
            await LoadSourceInstancesAsync();
        }
        finally
        {
            _deletingSourceInstanceId = null;
        }
    }

    private async Task TryRunAvailabilitySyncAsync(string successMessage, string failedPrefix)
    {
        try
        {
            await AvailabilitySyncService.RunSyncForOwnerAsync(Id, CancellationToken.None);
            _sourceMessage = successMessage;
            _sourceMessageIntent = MessageIntent.Success;
        }
        catch (InvalidOperationException syncEx)
        {
            _sourceMessage = $"{failedPrefix}: {syncEx.Message}";
            _sourceMessageIntent = MessageIntent.Warning;
        }
    }

    private static string? NormalizeJsonInput(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task InvokePluginActionAsync(SourceInstanceEditor instance,
        CalendarSourcePluginActionDescriptor action)
    {
        _sourceMessage = null;
        _lastActionInstanceId = instance.Id;
        _executingActionInstanceId = instance.Id;
        _executingActionId = action.ActionId;
        StateHasChanged();

        try
        {
            var baseUri = Navigation.BaseUri.TrimEnd('/');
            var callbackUri = $"{baseUri}/consent-callback";

            string authUrl;
            switch (action.ActionId)
            {
                case "google-instance-consent":
                    authUrl = await GoogleConsentService.BuildAuthorizationUrlAsync(Id, instance.Id, callbackUri);
                    Navigation.NavigateTo(authUrl, forceLoad: true);
                    break;

                case "graph-instance-consent":
                    authUrl = await GraphConsentService.BuildAuthorizationUrlAsync(Id, instance.Id, callbackUri);
                    Navigation.NavigateTo(authUrl, forceLoad: true);
                    break;

                default:
                    _sourceMessage = $"Action '{action.ActionId}' is not handled by this version of ObfusCal.";
                    _sourceMessageIntent = MessageIntent.Warning;
                    break;
            }
        }
        catch (Exception ex)
        {
            _sourceMessage = $"Could not start action '{action.Label}': {ex.Message}";
            _sourceMessageIntent = MessageIntent.Error;
        }
        finally
        {
            _executingActionInstanceId = null;
            _executingActionId = null;
        }
    }
}
