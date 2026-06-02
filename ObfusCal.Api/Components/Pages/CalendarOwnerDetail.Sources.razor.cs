using Microsoft.FluentUI.AspNetCore.Components;
using ObfusCal.Api.Components.CalendarOwnerDetail;
using ObfusCal.Application.Interfaces;
using System.Net.Http;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    private string NewSourceAutomaticColorHex
    {
        get
        {
            var candidateLabels = _sourceInstances
                .Select(instance => instance.DisplayName)
                .Concat(GetNewSourceDisplayNameCandidates());

            return CalendarColorFieldDisplay.ResolveAutomaticColor(
                GetPendingNewSourceDisplayName(),
                candidateLabels);
        }
    }

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
                ui?.SetupHint,
                ui?.Actions ?? []));
        }

        _selectedPluginOption = _pluginOptions.FirstOrDefault();
        ApplyPluginDefaults();
    }

    private async Task LoadSourceInstancesAsync()
    {
        _sourceInstances.Clear();
        var instances = await CalendarSourceInstanceService.ListAsync(Id);
        var automaticColorsByDisplayName = instances
            .Select(instance => instance.DisplayName)
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                displayName => displayName,
                displayName => CalendarColorFieldDisplay.ResolveAutomaticColor(
                    displayName,
                    instances.Select(source => source.DisplayName)),
                StringComparer.Ordinal);

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
                ColorHex = instance.ColorHex,
                AutomaticColorHex = automaticColorsByDisplayName[instance.DisplayName],
                IsEnabled = instance.IsEnabled,
                IsReady = instance.IsReady,
                Title = instance.Title,
                Detail = instance.Detail,
                SetupHint = plugin?.SetupHint,
                ConfigurationJson = instance.ConfigurationJson,
                SecretDataJson = string.Empty,
                ConfigurationFields = configurationFields,
                SecretFields = secretFields,
                Actions = actions
            });
        }

        _hasWriteBackCapableSource = _sourceInstances.Any(instance =>
        {
            if (!instance.IsEnabled)
                return false;
            var plugin = CalendarSourceCatalog.GetPlugin(instance.PluginId);
            return plugin is not null
                && typeof(ICalendarWriteBack).IsAssignableFrom(plugin.ImplementationType);
        });
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
        _newSourceColorHex = null;
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

        var selectedPlugin = _selectedPluginOption;

        if (!selectedPlugin.SupportsMultipleInstances
            && _sourceInstances.Any(instance =>
                string.Equals(instance.PluginId, selectedPlugin.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _sourceMessage = $"{selectedPlugin.DisplayName} supports only one source instance.";
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
                    selectedPlugin.Id,
                    string.IsNullOrWhiteSpace(_newSourceDisplayName)
                        ? selectedPlugin.DisplayName
                        : _newSourceDisplayName,
                    configurationJson,
                    secretDataJson,
                    _newSourceIsEnabled,
                    _newSourceColorHex));

            if (created is null)
            {
                _sourceMessage = "Unable to create source instance.";
                _sourceMessageIntent = MessageIntent.Error;
                return;
            }

            var sourceInstancesReloaded = false;
            try
            {
                await LoadSourceInstancesAsync();
                sourceInstancesReloaded = true;
            }
            catch (HttpRequestException) { /* stale list is acceptable */ }
            catch (TaskCanceledException) { /* stale list is acceptable */ }

            var authAction = CalendarSourceAuthFlowService.GetAuthenticationAction(selectedPlugin.Actions);
            if (authAction is null)
            {
                _showAddForm = false;
                ApplyPluginDefaults();
                await TryRunAvailabilitySyncAsync(
                    $"Added source instance '{created.DisplayName}' and synced availability.",
                    $"Added source instance '{created.DisplayName}', but sync failed");
                return;
            }

            await StartSourceAuthenticationAsync(created.Id, created.DisplayName, authAction, sourceInstancesReloaded);
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
                    instance.IsEnabled,
                    instance.ColorHex ?? string.Empty));

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
            catch (InvalidOperationException) { /* stale list is acceptable; the snapshot sync still fires */ }
            catch (TaskCanceledException) { /* stale list is acceptable; the snapshot sync still fires */ }

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
            _expandedSourceInstanceId = null;
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

    private IEnumerable<string> GetNewSourceDisplayNameCandidates()
    {
        var pendingDisplayName = GetPendingNewSourceDisplayName();
        if (!string.IsNullOrWhiteSpace(pendingDisplayName))
            yield return pendingDisplayName;
    }

    private string? GetPendingNewSourceDisplayName()
        => string.IsNullOrWhiteSpace(_newSourceDisplayName)
            ? _selectedPluginOption?.DisplayName
            : _newSourceDisplayName.Trim();

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

            var authUrl = await CalendarSourceAuthFlowService.BuildAuthorizationUrlAsync(
                Id,
                instance.Id,
                action.ActionId,
                callbackUri);
            Navigation.NavigateTo(authUrl, forceLoad: true);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Unknown auth action", StringComparison.Ordinal))
        {
            _sourceMessage = $"Action '{action.ActionId}' is not handled by this version of ObfusCal.";
            _sourceMessageIntent = MessageIntent.Warning;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
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

    private async Task StartSourceAuthenticationAsync(
        Guid sourceInstanceId,
        string displayName,
        CalendarSourcePluginActionDescriptor authAction,
        bool sourceInstancesReloaded)
    {
        try
        {
            var baseUri = Navigation.BaseUri.TrimEnd('/');
            var callbackUri = $"{baseUri}/consent-callback";
            var authUrl = await CalendarSourceAuthFlowService.BuildAuthorizationUrlAsync(
                Id,
                sourceInstanceId,
                authAction.ActionId,
                callbackUri);
            _showAddForm = false;
            ApplyPluginDefaults();
            Navigation.NavigateTo(authUrl, forceLoad: true);
        }
        catch (InvalidOperationException ex) when (ex.Message.StartsWith("Unknown auth action", StringComparison.Ordinal))
        {
            HandleSourceAuthenticationStartFailure(
                sourceInstanceId,
                sourceInstancesReloaded,
                $"Added source instance '{displayName}', but authentication could not be started because action '{authAction.ActionId}' is not supported by this version of ObfusCal.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException)
        {
            HandleSourceAuthenticationStartFailure(
                sourceInstanceId,
                sourceInstancesReloaded,
                $"Added source instance '{displayName}', but authentication could not be started: {ex.Message}");
        }
    }

    private void HandleSourceAuthenticationStartFailure(
        Guid sourceInstanceId,
        bool sourceInstancesReloaded,
        string message)
    {
        _showAddForm = false;
        _expandedSourceInstanceId = sourceInstancesReloaded ? sourceInstanceId : null;
        _sourceMessage = message;
        _sourceMessageIntent = MessageIntent.Warning;
        ApplyPluginDefaults();
    }
}
