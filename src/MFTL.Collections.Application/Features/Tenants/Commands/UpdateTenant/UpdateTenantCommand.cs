using MediatR;

namespace MFTL.Collections.Application.Features.Tenants.Commands.UpdateTenant;

public record UpdateTenantCommand : IRequest<bool>
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? SupportEmail { get; init; }
}
