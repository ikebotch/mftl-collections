using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using MediatR;
using MFTL.Collections.Api.Extensions;
using MFTL.Collections.Contracts.Common;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Application.Features.SmsTemplates.Commands.CreateSmsTemplate;
using MFTL.Collections.Application.Features.SmsTemplates.Commands.UpdateSmsTemplate;
using MFTL.Collections.Application.Features.SmsTemplates.Queries.GetSmsTemplateById;
using MFTL.Collections.Application.Features.SmsTemplates.Queries.ListSmsTemplates;
using System.Text.Json;

namespace MFTL.Collections.Api.Functions.SmsTemplates;

public class SmsTemplateFunctions(IMediator mediator)
{
    [Function("CreateSmsTemplate")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ApiRoutes.SmsTemplates.Base)] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = JsonSerializer.Deserialize<CreateSmsTemplateCommand>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command);
        return new OkObjectResult(new ApiResponse<Guid>(true, "SMS Template created.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("UpdateSmsTemplate")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = ApiRoutes.SmsTemplates.Update)] HttpRequest req, Guid id)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var command = JsonSerializer.Deserialize<UpdateSmsTemplateCommand>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
        if (command == null) return new BadRequestObjectResult(new ApiResponse(false, "Invalid body.", CorrelationId: req.GetOrCreateCorrelationId()));

        var result = await mediator.Send(command with { Id = id });
        if (!result) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<bool>(true, "SMS Template updated.", result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("GetSmsTemplateById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.SmsTemplates.GetById)] HttpRequest req, Guid id)
    {
        var result = await mediator.Send(new GetSmsTemplateByIdQuery(id));
        if (result == null) return new NotFoundResult();
        
        return new OkObjectResult(new ApiResponse<SmsTemplateDto>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }

    [Function("ListSmsTemplates")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ApiRoutes.SmsTemplates.Base)] HttpRequest req)
    {
        var result = await mediator.Send(new ListSmsTemplatesQuery());
        return new OkObjectResult(new ApiResponse<List<SmsTemplateDto>>(true, Data: result, CorrelationId: req.GetOrCreateCorrelationId()));
    }
}
