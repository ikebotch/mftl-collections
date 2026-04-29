using System.Reflection;
using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Common.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse>(
    IPermissionEvaluator permissionEvaluator) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var authorizeAttributes = request.GetType().GetCustomAttributes<HasPermissionAttribute>();

        if (authorizeAttributes.Any())
        {
            Guid? scopeId = null;
            if (request is IHasScope scopedRequest)
            {
                scopeId = scopedRequest.GetScopeId();
            }

            foreach (var attribute in authorizeAttributes)
            {
                var authorized = await permissionEvaluator.HasPermissionAsync(attribute.Permission, scopeId);

                if (!authorized)
                {
                    throw new UnauthorizedAccessException($"User does not have the required permission: {attribute.Permission}");
                }
            }
        }

        return await next();
    }
}
