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
                var httpContext = context.GetHttpContext();
                if (httpContext != null)
                {
                    var statusCode = ex switch
                    {
                        KeyNotFoundException => (int)HttpStatusCode.NotFound,
                        InvalidOperationException => (int)HttpStatusCode.BadRequest,
                        _ => (int)HttpStatusCode.InternalServerError,
                    };

                    var message = ex switch
                    {
                        KeyNotFoundException => ex.Message,
                        InvalidOperationException => ex.Message,
                        _ => "An internal server error occurred.",
                    };

                    httpContext.Response.StatusCode = statusCode;
                    httpContext.Response.ContentType = "application/json";
                    
                    var envelope = new ApiResponse(false, message, new[] { ex.Message }, httpContext.TraceIdentifier);
                    var json = JsonSerializer.Serialize(envelope);
                    await httpContext.Response.Body.WriteAsync(System.Text.Encoding.UTF8.GetBytes(json));
                }
            }
        }
    }
}
