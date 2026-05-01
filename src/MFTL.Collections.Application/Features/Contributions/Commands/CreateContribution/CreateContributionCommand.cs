using MediatR;
using MFTL.Collections.Application.Common.Interfaces;
using MFTL.Collections.Contracts.Responses;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace MFTL.Collections.Application.Features.Contributions.Commands.CreateContribution;

public record CreateContributionCommand(
    Guid EventId,
    Guid RecipientFundId,
    decimal Amount,
    string Currency,
    string? ContributorName,
    string ContributorPhone,
    string? ContributorEmail,
    bool Anonymous,
    string PaymentMethod,
    string? Note,
    string? Reference) : IRequest<ContributionDto>;

public class CreateContributionCommandValidator : AbstractValidator<CreateContributionCommand>
{
    public CreateContributionCommandValidator()
    {
        RuleFor(v => v.EventId).NotEmpty();
        RuleFor(v => v.RecipientFundId).NotEmpty();
        RuleFor(v => v.Amount).GreaterThan(0);
        RuleFor(v => v.Currency).NotEmpty();
        RuleFor(v => v.ContributorPhone).NotEmpty().MinimumLength(7);
        RuleFor(v => v.PaymentMethod).NotEmpty();
        
        When(v => !v.Anonymous, () =>
        {
            RuleFor(v => v.ContributorName).NotEmpty().MinimumLength(2);
        });
    }
}

public class CreateContributionCommandHandler(
    IApplicationDbContext dbContext) : IRequestHandler<CreateContributionCommand, ContributionDto>
{
    public async Task<ContributionDto> Handle(CreateContributionCommand request, CancellationToken cancellationToken)
    {
        var @event = await dbContext.Events
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == request.EventId, cancellationToken);

        if (@event == null) throw new KeyNotFoundException("Event not found.");
        if (!@event.IsActive) throw new InvalidOperationException("Event is not active.");

        var recipientFund = await dbContext.RecipientFunds
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == request.RecipientFundId && f.EventId == request.EventId, cancellationToken);

        if (recipientFund == null) throw new KeyNotFoundException("Recipient fund not found in the specified event.");

        var contributor = new Contributor
        {
            TenantId = @event.TenantId,
            BranchId = @event.BranchId,
            Name = request.Anonymous ? "Anonymous" : request.ContributorName?.Trim() ?? "Anonymous",
            Email = request.ContributorEmail?.Trim() ?? string.Empty,
            PhoneNumber = request.ContributorPhone.Trim(),
            IsAnonymous = request.Anonymous,
        };

        dbContext.Contributors.Add(contributor);

        var contribution = new Contribution
        {
            TenantId = @event.TenantId,
            BranchId = @event.BranchId,
            EventId = request.EventId,
            RecipientFundId = request.RecipientFundId,
            Contributor = contributor,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            ContributorName = contributor.Name,
            Method = request.PaymentMethod,
            Status = ContributionStatus.Pending,
            Note = request.Note,
            Reference = request.Reference ?? $"REF-{Guid.NewGuid():N}"
        };

        dbContext.Contributions.Add(contribution);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new ContributionDto(
            contribution.Id,
            contribution.EventId,
            contribution.RecipientFundId,
            contribution.Amount,
            contribution.Currency,
            contribution.ContributorName,
            contribution.Method,
            contribution.Status.ToString(),
            null,
            null,
            contribution.Note);
    }
}
