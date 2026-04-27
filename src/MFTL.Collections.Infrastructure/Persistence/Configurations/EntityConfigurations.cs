using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MFTL.Collections.Domain.Entities;

namespace MFTL.Collections.Infrastructure.Persistence.Configurations;

public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.BranchId);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}

public class ContributionConfiguration : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.Reference).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasOne(x => x.Payment)
            .WithMany()
            .HasForeignKey(x => x.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.Receipt)
            .WithOne(x => x.Contribution)
            .HasForeignKey<Receipt>(x => x.ContributionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ProviderPayload).HasColumnType("jsonb");
        builder.HasIndex(x => x.ProviderReference).IsUnique();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasOne(x => x.Receipt)
            .WithOne(x => x.Payment)
            .HasForeignKey<Receipt>(x => x.PaymentId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.ReceiptNumber).IsUnique();
        builder.HasIndex(x => x.ContributionId).IsUnique();
        builder.Property(x => x.ReceiptNumber).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.Metadata).HasColumnType("jsonb");
        builder.HasOne(x => x.Event)
            .WithMany(x => x.Receipts)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecipientFund)
            .WithMany(x => x.Receipts)
            .HasForeignKey(x => x.RecipientFundId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedByUser)
            .WithMany(x => x.RecordedReceipts)
            .HasForeignKey(x => x.RecordedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class ProcessedWebhookEventConfiguration : IEntityTypeConfiguration<ProcessedWebhookEvent>
{
    public void Configure(EntityTypeBuilder<ProcessedWebhookEvent> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.Provider, x.EventId }).IsUnique();
    }
}
