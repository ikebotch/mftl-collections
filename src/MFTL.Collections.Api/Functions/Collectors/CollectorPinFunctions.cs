using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;
using MFTL.Collections.Application.Features.Collectors.Commands.SetupPin;
using MFTL.Collections.Application.Features.Collectors.Commands.VerifyPin;
using MFTL.Collections.Application.Features.Collectors.Queries.GetPinStatus;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Responses;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Collectors;

public class CollectorPinFunctions(IMediator mediator)
{
    [Function("GetCollectorPinStatus")]
    public async Task<IActionResult> GetStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Collectors.PinStatus)] HttpRequest req)
    {
        try
        {
            var result = await mediator.Send(new GetPinStatusQuery());
            return new OkObjectResult(new ApiResponse<CollectorPinStatusResponse>(true, "PIN status retrieved.", result));
        }
        catch (UnauthorizedAccessException)
        {
            return new ObjectResult(new ApiResponse(false, "You do not have collector access in this tenant.")) { StatusCode = 403 };
        }
        catch (Exception)
        {
            return new ObjectResult(new ApiResponse(false, "Could not load your collection PIN. Please try again.")) { StatusCode = 500 };
        }
    }

    [Function("SetupCollectorPin")]
    public async Task<IActionResult> Setup(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Collectors.PinSetup)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<SetupCollectorPinRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        try
        {
            var result = await mediator.Send(new SetupCollectorPinCommand(request.Pin));
            return new OkObjectResult(new ApiResponse<CollectorPinStatusResponse>(true, "PIN setup successful.", result));
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(new ApiResponse(false, ex.Message));
        }
        catch (UnauthorizedAccessException)
        {
            return new ObjectResult(new ApiResponse(false, "You do not have collector access in this tenant.")) { StatusCode = 403 };
        }
        catch (Exception)
        {
            return new ObjectResult(new ApiResponse(false, "Could not set up your collection PIN. Please try again.")) { StatusCode = 500 };
        }
    }

    [Function("VerifyCollectorPin")]
    public async Task<IActionResult> Verify(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Collectors.PinVerify)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<VerifyCollectorPinRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body."));

        try
        {
            var result = await mediator.Send(new VerifyCollectorPinCommand(request.Pin));
            if (result.Verified == true)
            {
                return new OkObjectResult(new ApiResponse<CollectorPinStatusResponse>(true, "PIN verified.", result));
            }
            return new BadRequestObjectResult(new ApiResponse<CollectorPinStatusResponse>(false, "Incorrect PIN.", result));
        }
        catch (UnauthorizedAccessException)
        {
            return new ObjectResult(new ApiResponse(false, "You do not have collector access in this tenant.")) { StatusCode = 403 };
        }
        catch (Exception)
        {
            return new ObjectResult(new ApiResponse(false, "Could not verify your collection PIN. Please try again.")) { StatusCode = 500 };
        }
    }
}
