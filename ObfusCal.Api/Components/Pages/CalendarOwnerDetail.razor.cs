using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    [Parameter] public Guid Id { get; set; }

    private CalendarOwnerInfo? _owner;
    private List<ProfileViewModel> _profiles = [];

    private readonly List<PluginOption> _pluginOptions = [];
    private readonly List<SourceInstanceEditor> _sourceInstances = [];
    private PluginOption? _selectedPluginOption;
    private string? _newSourceDisplayName;
    private string? _newSourceConfigurationJson;
    private string? _newSourceSecretDataJson;
    private List<PluginFieldEditor> _newSourceConfigurationFields = [];
    private List<PluginFieldEditor> _newSourceSecretFields = [];
    private bool _newSourceIsEnabled = true;
    private bool _creatingSourceInstance;
    private Guid? _updatingSourceInstanceId;
    private Guid? _deletingSourceInstanceId;
    private Guid? _executingActionInstanceId;
    private string? _executingActionId;
    private bool _showAddForm;
    private string? _sourceMessage;
    private MessageIntent _sourceMessageIntent = MessageIntent.Info;
    private Guid? _lastActionInstanceId;


    private string? _profileMessage;

    private bool _triggeringSyncForOwner;
    private string? _ownerSyncMessage;
    private MessageIntent _ownerSyncMessageIntent = MessageIntent.Info;

    private PluginOption? SelectedPluginOption
    {
        get => _selectedPluginOption;
        set
        {
            _selectedPluginOption = value;
            ApplyPluginDefaults();
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _owner = await CalendarOwnerService.GetByIdAsync(Id);
        if (_owner is null)
            return;

        LoadPluginCatalog();
        await LoadSourceInstancesAsync();
        await LoadProfilesAsync();
    }
}
