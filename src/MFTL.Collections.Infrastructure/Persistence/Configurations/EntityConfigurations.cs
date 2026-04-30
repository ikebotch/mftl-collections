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
        builder.HasIndex(x => x.TenantId);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
    }
}

public class ContributionConfiguration : IEntityTypeConfiguration<Contribution>
{
    public void Configure(EntityTypeBuilder<Contribution> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
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
        builder.HasIndex(x => x.TenantId);
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
        builder.HasOne<Tenant>()
            .WithMany(x => x.Receipts)
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
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

public class ProcessedExternalPaymentCallbackConfiguration : IEntityTypeConfiguration<ProcessedExternalPaymentCallback>
{
    public void Configure(EntityTypeBuilder<ProcessedExternalPaymentCallback> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.PaymentServicePaymentId).IsUnique();
        builder.Property(x => x.PaymentServicePaymentId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Provider).HasMaxLength(64);
        builder.Property(x => x.ProviderReference).HasMaxLength(256);
        builder.Property(x => x.ProviderTransactionId).HasMaxLength(256);
        builder.Property(x => x.ExternalReference).HasMaxLength(256);
        builder.Property(x => x.PayloadHash).HasMaxLength(256);
        builder.Property(x => x.Status).HasMaxLength(64);
    }
}

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.TenantId, x.TemplateKey, x.Channel }).IsUnique();
        
        builder.Property(x => x.TemplateKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Channel).IsRequired().HasMaxLength(32);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Body).IsRequired();
        
        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.OutboxMessageId);
        builder.Property(x => x.Channel);
        builder.Property(x => x.Status);
        builder.Property(x => x.TemplateKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ReceiptId);
        builder.Property(x => x.PaymentId);
        builder.Property(x => x.ContributionId);
        builder.Property(x => x.Recipient).IsRequired().HasMaxLength(256);
        builder.Property(x => x.RecipientPhone).HasMaxLength(64);
        builder.Property(x => x.RecipientEmail).HasMaxLength(256);
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(256);
        builder.Property(x => x.Error).HasMaxLength(1000);
    }
}

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => x.BranchId);
        builder.HasIndex(x => x.AggregateId);
        builder.HasIndex(x => new { x.Status, x.NextAttemptAt, x.CreatedAt });
        builder.Property(x => x.AggregateType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EventType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Payload).HasColumnName("PayloadJson").IsRequired();
        builder.Property(x => x.CorrelationId).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Status);
        builder.Property(x => x.LastError).HasMaxLength(2000);
    }
}
