using MediatR;
using MFTL.Collections.Application.Features.Events.Queries.GetEventBySlug;
using MFTL.Collections.Application.Features.RecipientFunds.Queries.ListRecipientFundsByEvent;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Requests;
using MFTL.Collections.Api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using MFTL.Collections.Application.Common.Interfaces;

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
        var e = await dbContext.Events.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Slug == slug);
        if (e == null)
        {
            return new NotFoundObjectResult(new ApiResponse(false, $"Event with slug '{slug}' not found.", CorrelationId: req.GetOrCreateCorrelationId()));
        }

        var result = await mediator.Send(new ListRecipientFundsByEventQuery(e.Id));
        return new OkObjectResult(new ApiResponse<IEnumerable<RecipientFundDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
