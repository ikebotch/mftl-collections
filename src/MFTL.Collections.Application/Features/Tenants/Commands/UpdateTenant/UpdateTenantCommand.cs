using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Application.Common.Security;

namespace MFTL.Collections.Application.Features.Tenants.Commands.UpdateTenant;

[HasPermission("organisations.update")]
public record UpdateTenantCommand : IRequest<bool>, IHasScope
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? SupportEmail { get; init; }

    public Guid? GetScopeId() => Id;
}
