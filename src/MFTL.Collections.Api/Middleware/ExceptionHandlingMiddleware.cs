using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using System.Text.Json;

namespace MFTL.Collections.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred during function execution.");

            if (context.IsHttpTrigger())
            {
                var request = await context.GetHttpRequestDataAsync();
                if (request != null)
                {
                    var statusCode = ex switch
                    {
                        KeyNotFoundException => HttpStatusCode.NotFound,
                        InvalidOperationException => HttpStatusCode.BadRequest,
                        _ => HttpStatusCode.InternalServerError,
                    };

                    var message = ex switch
                    {
                        KeyNotFoundException => ex.Message,
                        InvalidOperationException => ex.Message,
                        _ => "An internal server error occurred.",
                    };

                    var response = request.CreateResponse(statusCode);
                    var envelope = new ApiResponse(false, message, new[] { ex.Message }, request.GetOrCreateCorrelationId());
                    
                    await response.WriteStringAsync(JsonSerializer.Serialize(envelope));
                    context.GetInvocationResult().Value = response;
                }
            }
        }
    }
}
