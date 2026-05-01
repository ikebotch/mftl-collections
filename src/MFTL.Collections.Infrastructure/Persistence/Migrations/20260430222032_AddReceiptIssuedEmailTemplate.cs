using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceiptIssuedEmailTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$ 
BEGIN 
    IF NOT EXISTS (SELECT 1 FROM ""NotificationTemplates"" WHERE ""TemplateKey"" = 'receipt.issued' AND ""Channel"" = 'Email') THEN
        INSERT INTO ""NotificationTemplates"" (
            ""Id"", 
            ""TenantId"", 
            ""TemplateKey"", 
            ""Channel"", 
            ""Name"", 
            ""Subject"", 
            ""Body"", 
            ""IsActive"", 
            ""IsSystemDefault"", 
            ""CreatedAt""
        ) VALUES (
            '10000000-0000-0000-0000-000000000011', 
            '00000000-0000-0000-0000-000000000000', 
            'receipt.issued', 
            'Email', 
            'Receipt Issued (Email)', 
            'Receipt {{ReceiptNumber}} - {{EventTitle}}', 
            '<p>Hello {{ContributorName}},</p><p>Thank you for your contribution to <strong>{{TenantName}}</strong>.</p><p><strong>Receipt Details:</strong></p><ul><li><strong>Receipt Number:</strong> {{ReceiptNumber}}</li><li><strong>Event:</strong> {{EventTitle}}</li><li><strong>Fund:</strong> {{FundName}}</li><li><strong>Amount:</strong> {{Currency}} {{Amount}}</li><li><strong>Reference:</strong> {{ContributionReference}}</li><li><strong>Issued At:</strong> {{IssuedAt}}</li></ul><p>Thank you for your support!</p><p>Regards,<br/>{{TenantName}} Team</p>', 
            true, 
            true, 
            NOW()
        );
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""NotificationTemplates"" WHERE ""Id"" = '10000000-0000-0000-0000-000000000011';");
        }
    }
}
