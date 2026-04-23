using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
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
                    var response = request.CreateResponse(HttpStatusCode.InternalServerError);
                    var envelope = new ApiResponse(false, "An internal server error occurred.", new[] { ex.Message });
                    
                    await response.WriteStringAsync(JsonSerializer.Serialize(envelope));
                    context.GetInvocationResult().Value = response;
                }
            }
        }
    }
}
