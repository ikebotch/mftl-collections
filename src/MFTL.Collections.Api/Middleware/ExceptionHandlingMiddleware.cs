using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;

namespace MFTL.Collections.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(
    ILogger<ExceptionHandlingMiddleware> logger, 
    IConfiguration configuration,
    IHostEnvironment environment) : IFunctionsWorkerMiddleware
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
            
            // Always log full exception server-side
            logger.LogError(exception, "An unhandled exception occurred during function execution.");

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

                    bool exposeDetailedErrors = configuration.GetValue<bool>("Api:ExposeDetailedErrors") || environment.IsDevelopment();

                    var message = GetUserFriendlyMessage(exception, context.FunctionDefinition.Name);
                    
                    var response = request.CreateResponse(statusCode);
                    
                    List<string>? errors = null;
                    if (exposeDetailedErrors)
                    {
                        errors = new List<string> { exception.Message };
                        if (exception.InnerException != null)
                        {
                            errors.Add(exception.InnerException.Message);
                            if (exception.InnerException.InnerException != null)
                            {
                                errors.Add(exception.InnerException.InnerException.Message);
                            }
                        }
                    }

                    var envelope = new ApiResponse(false, message, errors, request.GetOrCreateCorrelationId());
                    
                    await response.WriteStringAsync(JsonSerializer.Serialize(envelope));
                    context.GetInvocationResult().Value = response;
                }
            }
        }
    }

    private string GetUserFriendlyMessage(Exception ex, string functionName)
    {
        if (ex is UnauthorizedAccessException)
            return "Authentication is required to access this resource.";

        if (ex is KeyNotFoundException || ex is InvalidOperationException)
            return ex.Message;

        // Specific context-aware generic messages
        if (functionName.Contains("RecordCashContribution", StringComparison.OrdinalIgnoreCase))
            return "Could not save contribution. Please try again.";

        if (functionName.Contains("Pin", StringComparison.OrdinalIgnoreCase))
            return "Could not load your collection PIN. Please try again.";

        return "An unexpected error occurred. Please try again.";
    }
}
