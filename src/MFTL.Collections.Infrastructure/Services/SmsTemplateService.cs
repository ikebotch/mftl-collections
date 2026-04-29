using System.Reflection;
using MFTL.Collections.Application.Common.Interfaces;

namespace MFTL.Collections.Infrastructure.Services;

public class SmsTemplateService : ISmsTemplateService
{
    public string Render(string template, object data)
    {
        if (string.IsNullOrWhiteSpace(template)) return string.Empty;
        if (data == null) return template;

        var result = template;
        var type = data.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var prop in properties)
        {
            var placeholder = "{" + prop.Name + "}";
            var value = prop.GetValue(data)?.ToString() ?? string.Empty;
            result = result.Replace(placeholder, value, StringComparison.OrdinalIgnoreCase);
        }

        return result;
    }
}
