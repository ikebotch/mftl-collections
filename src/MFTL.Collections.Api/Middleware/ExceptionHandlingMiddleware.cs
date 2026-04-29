using System;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using System.Text.Json;
using System.Reflection;
using System.Linq;

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
            var exception = ex;
            
            if (ex is AggregateException aggEx)
            {
                exception = aggEx.Flatten().InnerExceptions.FirstOrDefault() ?? ex;
            }
            else if (ex is TargetInvocationException targetEx && targetEx.InnerException != null)
            {
                exception = targetEx.InnerException;
            }
            
            if (exception is UnauthorizedAccessException)
            {
                logger.LogWarning(exception, "UnauthorizedAccessException caught in middleware. Returning 401.");
            }
            else 
            {
                logger.LogError(exception, "An unhandled exception occurred during function execution.");
            }

            if (context.IsHttpTrigger())
            {
                var request = await context.GetHttpRequestDataAsync();
                if (request != null)
                {
                    var statusCode = exception switch
                    {
                        KeyNotFoundException => HttpStatusCode.NotFound,
                        InvalidOperationException => HttpStatusCode.BadRequest,
                        UnauthorizedAccessException => HttpStatusCode.Unauthorized,
                        _ => HttpStatusCode.InternalServerError,
                    };

                    var message = exception switch
                    {
                        KeyNotFoundException => exception.Message,
                        InvalidOperationException => exception.Message,
                        UnauthorizedAccessException => "Authentication is required to access this resource.",
                        _ => "An internal server error occurred.",
                    };

                    var details = new[] { 
                        exception.Message, 
                        $"Type: {exception.GetType().Name}",
                        exception.StackTrace ?? ""
                    };

                    var response = request.CreateResponse(statusCode);
                    var envelope = new ApiResponse(false, message, details, request.GetOrCreateCorrelationId());
                    
                    await response.WriteStringAsync(JsonSerializer.Serialize(envelope));
                    context.GetInvocationResult().Value = response;
                }
            }
        }
    }
}
