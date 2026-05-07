using ObfusCal.Application.Interfaces;
using ObfusCal.Application.Obfuscation;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    private sealed class ProfileViewModel
    {
        public ObfuscationAuditContext Context { get; set; }
        public bool RemoveTitle { get; set; }
        public bool RemoveDescription { get; set; }
        public bool RemoveLocation { get; set; }
        public bool RemoveAttendees { get; set; }
        public bool RoundTimes { get; set; }
        public int RoundingIntervalMinutes { get; set; }
        public bool MergeBlocks { get; set; }
    }

    private sealed record PluginOption(
        string Id,
        string DisplayName,
        bool IsExternalPlugin,
        bool SupportsMultipleInstances,
        string? ConfigurationJsonTemplate,
        string? SecretDataJsonTemplate,
        string? SetupHint)
    {
        public string DisplayLabel => IsExternalPlugin ? $"{DisplayName} (plugin)" : DisplayName;
    }

    private sealed class PluginFieldEditor
    {
        public required string Key { get; init; }
        public required string Label { get; init; }
        public string? Placeholder { get; init; }
        public string? Value { get; set; }
    }

    private sealed class SourceInstanceEditor
    {
        public Guid Id { get; init; }
        public required string PluginId { get; init; }
        public required string PluginDisplayName { get; init; }
        public required string DisplayName { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsReady { get; set; }
        public required string Title { get; init; }
        public string? Detail { get; init; }
        public string? ConfigurationJson { get; set; }
        public string? SecretDataJson { get; set; }
        public List<PluginFieldEditor> ConfigurationFields { get; init; } = [];
        public List<PluginFieldEditor> SecretFields { get; init; } = [];
        public IReadOnlyList<CalendarSourcePluginActionDescriptor> Actions { get; init; } = [];
    }
}
