using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Services;

public sealed partial class SmsTemplateService(ILogger<SmsTemplateService> logger) : ISmsTemplateService
{
    public string Render(string template, object data)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        if (data == null) return template;

        var values = Flatten(data);
        var missingVariables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var rendered = DoubleBracePattern().Replace(template, match => ReplaceMatch(match, values, missingVariables));
        rendered = SingleBracePattern().Replace(rendered, match => ReplaceMatch(match, values, missingVariables));

        if (missingVariables.Count > 0)
        {
            logger.LogDebug("Template rendering skipped missing variables: {Variables}", string.Join(", ", missingVariables));
        }

        return rendered;
    }

    private static string ReplaceMatch(Match match, IReadOnlyDictionary<string, object?> values, ISet<string> missingVariables)
    {
        var key = match.Groups["key"].Value;
        if (values.TryGetValue(key, out var value))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        missingVariables.Add(key);
        return match.Value;
    }

    private static Dictionary<string, object?> Flatten(object data)
    {
        return data switch
        {
            JsonElement jsonElement => FlattenJsonElement(jsonElement),
            IDictionary<string, object?> dict => new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase),
            IDictionary dictionary => FlattenDictionary(dictionary),
            _ => FlattenObject(data)
        };
    }

    private static Dictionary<string, object?> FlattenDictionary(IDictionary dictionary)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Key is string key)
            {
                values[key] = entry.Value;
            }
        }

        return values;
    }

    private static Dictionary<string, object?> FlattenObject(object data)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var properties = data.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            values[property.Name] = property.GetValue(data);
        }

        return values;
    }

    private static Dictionary<string, object?> FlattenJsonElement(JsonElement element)
    {
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (element.ValueKind != JsonValueKind.Object)
        {
            return values;
        }

        foreach (var property in element.EnumerateObject())
        {
            values[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.Null => null,
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.ToString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => property.Value.ToString()
            };
        }

        return values;
    }

    [GeneratedRegex(@"\{\{\s*(?<key>\w+)\s*\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex DoubleBracePattern();

    [GeneratedRegex(@"(?<!\{)\{\s*(?<key>\w+)\s*\}(?!\})", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex SingleBracePattern();
}
