using MediatR;
using MFTL.Collections.Application.Features.Events.Queries.GetEventBySlug;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Features.Storefront.Commands.CreateStorefrontContribution;
using MFTL.Collections.Application.Features.Storefront.Queries.GetContributionStatus;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.Storefront;

public class StorefrontFunctions(IMediator mediator, IApplicationDbContext dbContext)
{
    [Function("Storefront_GetEventBySlug")]
    public async Task<IActionResult> GetEventBySlug(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Storefront.GetEventBySlug)] HttpRequest req, string slug)
    {
        try
        {
            var result = await mediator.Send(new GetEventBySlugQuery(slug));
            return new OkObjectResult(new ApiResponse<EventDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new ApiResponse(false, ex.Message, CorrelationId: req.GetOrCreateCorrelationId()));
        }
    }

    [Function("Storefront_ListFundsByEventSlug")]
    public async Task<IActionResult> ListFundsByEventSlug(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Storefront.ListFundsByEventSlug)] HttpRequest req, string slug)
    {
        // Must only return active events to public storefront.
        var e = await dbContext.Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive);

        if (e == null)
        {
            return new NotFoundObjectResult(new ApiResponse(false, $"Event with slug '{slug}' not found or inactive.", CorrelationId: req.GetOrCreateCorrelationId()));
        }

        var result = await mediator.Send(new ListRecipientFundsByEventQuery(e.Id));
        return new OkObjectResult(new ApiResponse<IEnumerable<RecipientFundDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("Storefront_CreateContribution")]
    public async Task<IActionResult> CreateContribution(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.Storefront.PostContribution)] HttpRequest req, string slug)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var request = JsonSerializer.Deserialize<CreateStorefrontContributionRequest>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (request == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        try
        {
            var command = new CreateStorefrontContributionCommand(
                slug,
                request.RecipientFundId,
                request.Amount,
                request.Currency,
                request.DonorName,
                request.DonorPhone,
                request.DonorEmail,
                request.Anonymous,
                request.PaymentMethod,
                request.DonorNetwork,
                request.Note);

            var result = await mediator.Send(command);
            return new OkObjectResult(new ApiResponse<StorefrontContributionResponse>(true, "Contribution initiated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new ApiResponse(false, ex.Message, CorrelationId: req.GetOrCreateCorrelationId()));
        }
        catch (InvalidOperationException ex)
        {
            return new UnprocessableEntityObjectResult(new ApiResponse(false, ex.Message, CorrelationId: req.GetOrCreateCorrelationId()));
        }
    }

    [Function("Storefront_GetContributionStatus")]
    public async Task<IActionResult> GetContributionStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.Storefront.GetContributionStatus)] HttpRequest req, Guid id)
    {
        try
        {
            var result = await mediator.Send(new GetStorefrontContributionStatusQuery(id));
            return new OkObjectResult(new ApiResponse<StorefrontContributionStatusDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
        }
        catch (KeyNotFoundException ex)
        {
            return new NotFoundObjectResult(new ApiResponse(false, ex.Message, CorrelationId: req.GetOrCreateCorrelationId()));
        }
    }
}
