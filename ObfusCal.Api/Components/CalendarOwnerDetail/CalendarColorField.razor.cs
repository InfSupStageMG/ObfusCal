using Microsoft.AspNetCore.Components;

namespace ObfusCal.Api.Components.CalendarOwnerDetail;

/// <summary>
/// Renders a lightweight preset-based calendar color selector.
/// </summary>
public partial class CalendarColorField
{
    private readonly string _generatedInputId = $"calendar-color-{Guid.NewGuid():N}";

    [Parameter]
    public string Label { get; set; } = "Calendar color";

    [Parameter]
    public string HintText { get; set; } = "Choose a preset or leave it on automatic to use the calendar palette.";

    [Parameter]
    public string? InputId { get; set; }

    [Parameter]
    public string? Value { get; set; }

    [Parameter]
    public string? AutomaticColorHex { get; set; }

    [Parameter]
    public EventCallback<string?> ValueChanged { get; set; }

    private string EffectiveInputId => string.IsNullOrWhiteSpace(InputId) ? _generatedInputId : InputId;

    private Task SelectAutomaticAsync()
        => UpdateValueAsync(null);

    private Task SelectPresetAsync(string color)
        => UpdateValueAsync(color);


    private Task UpdateValueAsync(string? value)
        => ValueChanged.InvokeAsync(string.IsNullOrWhiteSpace(value) ? null : value.Trim());
}

