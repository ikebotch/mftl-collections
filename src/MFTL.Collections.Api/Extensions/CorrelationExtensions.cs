using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker.Http;

namespace MFTL.Collections.Api.Extensions;

public static class CorrelationExtensions
{
    public const string CorrelationHeaderName = "x-correlation-id";

    public static string GetOrCreateCorrelationId(this HttpRequest request)
    {
        if (request.Headers.TryGetValue(CorrelationHeaderName, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.IsNullOrWhiteSpace(request.HttpContext.TraceIdentifier)
            ? Guid.NewGuid().ToString()
            : request.HttpContext.TraceIdentifier;
    }

    public static string GetOrCreateCorrelationId(this HttpRequestData request)
    {
        if (request.Headers.TryGetValues(CorrelationHeaderName, out var values))
        {
            var value = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return request.FunctionContext.InvocationId;
    }
}
