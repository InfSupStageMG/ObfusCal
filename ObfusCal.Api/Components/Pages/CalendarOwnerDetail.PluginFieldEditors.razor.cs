using System.Text.Json;
using System.Text.RegularExpressions;

namespace ObfusCal.Api.Components.Pages;

public partial class CalendarOwnerDetail
{
    private static List<PluginFieldEditor> BuildFieldEditorsFromTemplate(string? templateJson, string? currentValuesJson = null)
    {
        if (!TryParseFlatTemplate(templateJson, out var fields))
            return [];

        if (!TryParseFlatValues(currentValuesJson, out var currentValues))
            return fields;

        foreach (var field in fields)
        {
            if (currentValues.TryGetValue(field.Key, out var value))
                field.Value = value;
        }

        return fields;
    }

    private static bool HasFieldEditors(IReadOnlyCollection<PluginFieldEditor> fields) => fields.Count > 0;

    private static string? SerializeFieldEditors(IReadOnlyCollection<PluginFieldEditor> fields)
    {
        if (fields.Count == 0)
            return null;

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (string.IsNullOrWhiteSpace(field.Value))
                continue;

            values[field.Key] = field.Value.Trim();
        }

        return values.Count == 0 ? null : JsonSerializer.Serialize(values);
    }

    private static bool TryParseFlatTemplate(string? templateJson, out List<PluginFieldEditor> fields)
    {
        fields = [];
        if (string.IsNullOrWhiteSpace(templateJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(templateJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    return false;

                fields.Add(new PluginFieldEditor
                {
                    Key = property.Name,
                    Label = HumanizeKey(property.Name),
                    Placeholder = GetTemplateValue(property.Value),
                    Value = null
                });
            }

            return fields.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryParseFlatValues(string? valuesJson, out Dictionary<string, string?> values)
    {
        values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(valuesJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(valuesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;

            foreach (var property in document.RootElement.EnumerateObject().Where(property => property.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array)))
            {
                values[property.Name] = GetTemplateValue(property.Value);
            }

            return values.Count > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? GetTemplateValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static string HumanizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return "Field";

        var label = key.Replace("_", " ");
        // Split on camelCase / PascalCase boundaries
        label = Regex.Replace(label, @"(?<=[a-z\d])(?=[A-Z])", " ");
        label = Regex.Replace(label, @"(?<=[A-Z]+)(?=[A-Z][a-z])", " ");
        // Normalise common abbreviations
        label = Regex.Replace(label, @"\bUrl\b", "URL");
        label = Regex.Replace(label, @"\bId\b", "ID");
        // Capitalise the first character
        label = char.ToUpperInvariant(label[0]) + label[1..];
        return label;
    }
}

