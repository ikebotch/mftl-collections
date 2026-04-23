using MediatR;

namespace MFTL.Collections.Application.Features.Reports.Commands.ExportContributionReport;

public record ExportContributionReportCommand(Guid EventId, string Format) : IRequest<byte[]>;

public class ExportContributionReportCommandHandler : IRequestHandler<ExportContributionReportCommand, byte[]>
{
    public Task<byte[]> Handle(ExportContributionReportCommand request, CancellationToken cancellationToken)
    {
        // Scaffold implementation: return a dummy CSV
        var csv = "Id,Amount,Status,Date\n1,100,Completed,2024-01-01";
        return Task.FromResult(System.Text.Encoding.UTF8.GetBytes(csv));
    }
}
