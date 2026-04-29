using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Admin.Notifications.Commands.RetryOutboxMessage;

public record RetryOutboxMessageCommand(Guid Id) : IRequest<bool>;

public class RetryOutboxMessageCommandHandler(IApplicationDbContext dbContext) : IRequestHandler<RetryOutboxMessageCommand, bool>
{
    public async Task<bool> Handle(RetryOutboxMessageCommand request, CancellationToken cancellationToken)
    {
        var message = await dbContext.OutboxMessages
            .FirstOrDefaultAsync(m => m.Id == request.Id, cancellationToken);

        if (message == null) return false;

        message.Status = OutboxMessageStatus.Pending;
        message.AttemptCount = 0;
        message.NextAttemptAt = null;
        message.LastError = null;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
