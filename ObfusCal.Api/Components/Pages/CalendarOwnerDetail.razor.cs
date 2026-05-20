using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.FluentUI.AspNetCore.Components;
using ObfusCal.Api.Authorization;
using ObfusCal.Application.Interfaces;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private CurrentUserContextAccessor CurrentUserContextAccessor { get; set; } = default!;

    [Parameter] public Guid Id { get; set; }

    private CalendarOwnerInfo? _owner;
    private bool _isSysadmin;
    private string? _accessDeniedMessage;
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
    private bool _hasWriteBackCapableSource;


    private string? _profileMessage;

    private bool _triggeringSyncForOwner;
    private string? _ownerSyncMessage;
    private MessageIntent _ownerSyncMessageIntent = MessageIntent.Info;

    private string BackLink => _isSysadmin ? "/calendar-owners" : "/";

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
        var currentUser = await CurrentUserContextAccessor.GetCurrentAsync();
        _isSysadmin = currentUser.IsSysadmin;

        if (!_isSysadmin)
        {
            if (currentUser.CalendarOwnerId is null)
            {
                _accessDeniedMessage = "No calendar owner mapping exists for your signed-in account.";
                return;
            }

            if (currentUser.CalendarOwnerId != Id)
            {
                _accessDeniedMessage = "You are not authorized to view this calendar owner.";
                return;
            }
        }

        _owner = await CalendarOwnerService.GetByIdAsync(Id);
        if (_owner is null)
        {
            _accessDeniedMessage = "Calendar owner was not found.";
            return;
        }

        _writeBackEnabled = _owner.WriteBackEnabled;
        _writeBackPlaceholderTitle = _owner.WriteBackPlaceholderTitle;

        LoadPluginCatalog();
        await LoadSourceInstancesAsync();
        await LoadProfilesAsync();
    }
}
