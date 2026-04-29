using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MFTL.Collections.Domain.Entities;
using MFTL.Collections.Domain.Enums;

namespace MFTL.Collections.Infrastructure.Persistence.Configurations;

public class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TemplateKey).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Subject).HasMaxLength(500);
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Uniqueness: one active template per tenant+branch+key+channel
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.TemplateKey, x.Channel })
               .IsUnique()
               .HasFilter("\"IsActive\" = true");

        // Seed system-default templates (TenantId = Guid.Empty, IsSystemDefault = true)
        var systemTenantId = Guid.Empty;
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        builder.HasData(
            // SMS templates
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000001"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "receipt.issued",
                Channel = NotificationChannel.Sms,
                Name = "Receipt Issued (SMS)",
                Subject = null,
                Body = "Thank you {{donorName}} for your contribution of {{currency}} {{amount}} to {{eventName}}. Receipt: {{receiptNumber}}",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000002"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "receipt.resend",
                Channel = NotificationChannel.Sms,
                Name = "Receipt Resend (SMS)",
                Subject = null,
                Body = "Hi {{donorName}}, here is a copy of receipt {{receiptNumber}} for {{currency}} {{amount}}. Thank you for supporting {{eventName}}.",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000003"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "cashdrop.approved",
                Channel = NotificationChannel.Sms,
                Name = "Cash Drop Approved (SMS)",
                Subject = null,
                Body = "Hi {{collectorName}}, your cash drop of {{currency}} {{amount}} has been approved.",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000004"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "collector.assigned",
                Channel = NotificationChannel.Sms,
                Name = "Collector Assigned (SMS)",
                Subject = null,
                Body = "Hi {{collectorName}}, you have been assigned to collect for {{eventName}}.",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            // Email templates
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000005"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "user.invited",
                Channel = NotificationChannel.Email,
                Name = "User Invited (Email)",
                Subject = "You have been invited to MFTL Collections",
                Body = "Hello {{name}},\n\nYou have been invited to MFTL Collections as a {{role}}.\n\nOpen {{inviteLink}} to continue.\n\nRegards,\nThe MFTL Collections Team",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000006"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "collector.assigned",
                Channel = NotificationChannel.Email,
                Name = "Collector Assigned (Email)",
                Subject = "You have been assigned to {{eventName}}",
                Body = "Hello {{collectorName}},\n\nYou have been assigned as a collector for {{eventName}}.\n\nPlease log in to the MFTL Collections app to begin.\n\nRegards,\nThe MFTL Collections Team",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000007"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "payment.failed",
                Channel = NotificationChannel.Email,
                Name = "Payment Failed (Email)",
                Subject = "Payment Failed - Action Required",
                Body = "Hello,\n\nA payment of {{currency}} {{amount}} from {{donorName}} has failed.\n\nReason: {{reason}}\n\nPlease follow up with the donor.\n\nRegards,\nMFTL Collections",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000008"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "cashdrop.submitted",
                Channel = NotificationChannel.Email,
                Name = "Cash Drop Submitted (Email)",
                Subject = "New Cash Drop Submitted",
                Body = "Hello,\n\nCollector {{collectorName}} has submitted a cash drop of {{currency}} {{amount}}.\n\nPlease review and approve in the MFTL Collections admin.\n\nRegards,\nMFTL Collections",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000009"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "eod.closed",
                Channel = NotificationChannel.Email,
                Name = "EOD Closed (Email)",
                Subject = "EOD Report Closed - {{branchName}}",
                Body = "Hello,\n\nThe end-of-day report for {{branchName}} has been closed.\n\nTotal collected: {{currency}} {{totalAmount}}\n\nRegards,\nMFTL Collections",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            },
            new NotificationTemplate
            {
                Id = new Guid("10000000-0000-0000-0000-000000000010"),
                TenantId = systemTenantId,
                BranchId = null,
                TemplateKey = "settlement.ready",
                Channel = NotificationChannel.Email,
                Name = "Settlement Ready (Email)",
                Subject = "Settlement Ready for Review",
                Body = "Hello,\n\nA settlement of {{currency}} {{amount}} for collector {{collectorName}} is ready for your review.\n\nSettlement ID: {{settlementId}}\n\nRegards,\nMFTL Collections",
                IsActive = true,
                IsSystemDefault = true,
                CreatedAt = now
            }
        );
    }
}
