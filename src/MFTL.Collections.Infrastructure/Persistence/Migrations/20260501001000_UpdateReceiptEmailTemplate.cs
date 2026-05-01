using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateReceiptEmailTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "NotificationTemplates"
                SET "Body" = '<!doctype html>
                <html>
                  <body style="margin:0;padding:0;background:#f6f7f9;font-family:Arial,Helvetica,sans-serif;color:#111827;">
                    <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#f6f7f9;padding:32px 16px;">
                      <tr>
                        <td align="center">
                          <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:640px;background:#ffffff;border-radius:16px;overflow:hidden;border:1px solid #e5e7eb;">
                            <tr>
                              <td style="padding:28px 32px;background:#111827;color:#ffffff;">
                                <h1 style="margin:0;font-size:22px;line-height:1.3;">Receipt {{ReceiptNumber}}</h1>
                                <p style="margin:8px 0 0;font-size:14px;color:#d1d5db;">Thank you for your contribution to {{OrganizationName}}.</p>
                              </td>
                            </tr>

                            <tr>
                              <td style="padding:32px;">
                                <p style="margin:0 0 16px;font-size:16px;">Hi {{ContributorName}},</p>

                                <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#374151;">
                                  We have received your contribution. Your receipt details are below.
                                </p>

                                <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="border-collapse:collapse;margin:0 0 24px;">
                                  <tr>
                                    <td style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#6b7280;font-size:14px;">Event</td>
                                    <td align="right" style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#111827;font-size:14px;font-weight:600;">{{EventTitle}}</td>
                                  </tr>
                                  <tr>
                                    <td style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#6b7280;font-size:14px;">Fund</td>
                                    <td align="right" style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#111827;font-size:14px;font-weight:600;">{{FundName}}</td>
                                  </tr>
                                  <tr>
                                    <td style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#6b7280;font-size:14px;">Amount</td>
                                    <td align="right" style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#111827;font-size:18px;font-weight:700;">{{Currency}} {{Amount}}</td>
                                  </tr>
                                  <tr>
                                    <td style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#6b7280;font-size:14px;">Reference</td>
                                    <td align="right" style="padding:12px 0;border-bottom:1px solid #e5e7eb;color:#111827;font-size:14px;font-weight:600;">{{ContributionReference}}</td>
                                  </tr>
                                  <tr>
                                    <td style="padding:12px 0;color:#6b7280;font-size:14px;">Issued At</td>
                                    <td align="right" style="padding:12px 0;color:#111827;font-size:14px;font-weight:600;">{{IssuedAt}}</td>
                                  </tr>
                                </table>

                                <p style="margin:0 0 24px;font-size:15px;line-height:1.6;color:#374151;">
                                  Please keep this email as confirmation of your contribution.
                                </p>

                                <p style="margin:0;font-size:15px;color:#111827;">
                                  Regards,<br/>
                                  {{OrganizationName}} Team
                                </p>
                              </td>
                            </tr>

                            <tr>
                              <td style="padding:20px 32px;background:#f9fafb;border-top:1px solid #e5e7eb;color:#6b7280;font-size:12px;line-height:1.5;">
                                This is an automated receipt from {{OrganizationName}}. If you have questions, please contact the organisation directly.
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>
                    </table>
                  </body>
                </html>'
                WHERE "TemplateKey" = 'receipt.issued' AND "Channel" = 'Email'; -- Email
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "NotificationTemplates"
                SET "Body" = 'Hi {{donorName}}, thank you for your contribution of {{currency}} {{amount}} to {{eventName}}.'
                WHERE "TemplateKey" = 'receipt.issued' AND "Channel" = 'Email';
                """);
        }
    }
}
