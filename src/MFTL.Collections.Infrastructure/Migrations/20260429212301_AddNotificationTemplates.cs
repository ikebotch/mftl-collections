using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MFTL.Collections.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotificationTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: true),
                    TemplateKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    ModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    ModifiedBy = table.Column<string>(type: "text", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationTemplates", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "NotificationTemplates",
                columns: new[] { "Id", "Body", "BranchId", "Channel", "CreatedAt", "CreatedBy", "Description", "IsActive", "IsSystemDefault", "ModifiedAt", "ModifiedBy", "Name", "Subject", "TemplateKey", "TenantId" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "Thank you {{donorName}} for your contribution of {{currency}} {{amount}} to {{eventName}}. Receipt: {{receiptNumber}}", null, "Sms", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Receipt Issued (SMS)", null, "receipt.issued", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "Hi {{donorName}}, here is a copy of receipt {{receiptNumber}} for {{currency}} {{amount}}. Thank you for supporting {{eventName}}.", null, "Sms", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Receipt Resend (SMS)", null, "receipt.resend", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "Hi {{collectorName}}, your cash drop of {{currency}} {{amount}} has been approved.", null, "Sms", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Cash Drop Approved (SMS)", null, "cashdrop.approved", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "Hi {{collectorName}}, you have been assigned to collect for {{eventName}}.", null, "Sms", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Collector Assigned (SMS)", null, "collector.assigned", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000005"), "Hello {{name}},\n\nYou have been invited to MFTL Collections as a {{role}}.\n\nOpen {{inviteLink}} to continue.\n\nRegards,\nThe MFTL Collections Team", null, "Email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "User Invited (Email)", "You have been invited to MFTL Collections", "user.invited", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000006"), "Hello {{collectorName}},\n\nYou have been assigned as a collector for {{eventName}}.\n\nPlease log in to the MFTL Collections app to begin.\n\nRegards,\nThe MFTL Collections Team", null, "Email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Collector Assigned (Email)", "You have been assigned to {{eventName}}", "collector.assigned", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000007"), "Hello,\n\nA payment of {{currency}} {{amount}} from {{donorName}} has failed.\n\nReason: {{reason}}\n\nPlease follow up with the donor.\n\nRegards,\nMFTL Collections", null, "Email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Payment Failed (Email)", "Payment Failed - Action Required", "payment.failed", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000008"), "Hello,\n\nCollector {{collectorName}} has submitted a cash drop of {{currency}} {{amount}}.\n\nPlease review and approve in the MFTL Collections admin.\n\nRegards,\nMFTL Collections", null, "Email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Cash Drop Submitted (Email)", "New Cash Drop Submitted", "cashdrop.submitted", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000009"), "Hello,\n\nThe end-of-day report for {{branchName}} has been closed.\n\nTotal collected: {{currency}} {{totalAmount}}\n\nRegards,\nMFTL Collections", null, "Email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "EOD Closed (Email)", "EOD Report Closed - {{branchName}}", "eod.closed", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10000000-0000-0000-0000-000000000010"), "Hello,\n\nA settlement of {{currency}} {{amount}} for collector {{collectorName}} is ready for your review.\n\nSettlement ID: {{settlementId}}\n\nRegards,\nMFTL Collections", null, "Email", new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, null, true, true, null, null, "Settlement Ready (Email)", "Settlement Ready for Review", "settlement.ready", new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_NotificationTemplates_TenantId_BranchId_TemplateKey_Channel",
                table: "NotificationTemplates",
                columns: new[] { "TenantId", "BranchId", "TemplateKey", "Channel" },
                unique: true,
                filter: "\"IsActive\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NotificationTemplates");
        }
    }
}
