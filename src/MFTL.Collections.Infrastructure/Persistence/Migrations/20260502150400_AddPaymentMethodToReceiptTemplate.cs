using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    public partial class AddPaymentMethodToReceiptTemplate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "NotificationTemplates"
                SET "Body" = REPLACE("Body", 
                    '<tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Fund / Cause</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{FundName}}</td>
                                </tr>',
                    '<tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Fund / Cause</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{FundName}}</td>
                                </tr>
                                <tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Payment Method</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{PaymentMethod}}</td>
                                </tr>')
                WHERE "TemplateKey" IN ('receipt.issued', 'receipt.resend') AND "Channel" = 'Email';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "NotificationTemplates"
                SET "Body" = REPLACE("Body", 
                    '<tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Payment Method</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{PaymentMethod}}</td>
                                </tr>',
                    '')
                WHERE "TemplateKey" IN ('receipt.issued', 'receipt.resend') AND "Channel" = 'Email';
                """);
        }
    }
}
