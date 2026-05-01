using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ModernizeReceiptEmailTemplate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "NotificationTemplates"
                SET "Body" = '<!DOCTYPE html>
                <html lang="en">
                <head>
                  <meta charset="UTF-8">
                  <meta name="viewport" content="width=device-width, initial-scale=1.0">
                  <title>Receipt {{ReceiptNumber}}</title>
                </head>
                <body style="margin: 0; padding: 0; background-color: #F8FAFC; font-family: -apple-system, BlinkMacSystemFont, ''Segoe UI'', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; color: #1E293B;">
                  <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #F8FAFC; padding: 40px 0;">
                    <tr>
                      <td align="center">
                        <!-- Container -->
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width: 600px; background-color: #FFFFFF; border-radius: 8px; overflow: hidden; box-shadow: 0 10px 15px -3px rgba(0, 0, 0, 0.1);">
                          
                          <!-- Header: Dark Navy -->
                          <tr>
                            <td style="background-color: #0F172A; padding: 32px;">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                <tr>
                                  <td width="50%">
                                    <!-- Purple MFTL Logo Block -->
                                    <table role="presentation" cellspacing="0" cellpadding="0" style="background-color: #7C3AED; border-radius: 6px;">
                                      <tr>
                                        <td style="padding: 10px 16px; color: #FFFFFF; font-weight: 800; font-size: 20px; letter-spacing: -0.02em;">
                                          MFTL
                                        </td>
                                      </tr>
                                    </table>
                                  </td>
                                  <td width="50%" align="right" style="color: #94A3B8; font-size: 11px; font-weight: 700; letter-spacing: 0.1em; text-transform: uppercase;">
                                    Contribution Receipt
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- Receipt Title Section -->
                          <tr>
                            <td style="padding: 48px 40px 32px;">
                              <h1 style="margin: 0; font-size: 32px; font-weight: 800; color: #0F172A; letter-spacing: -0.03em; line-height: 1;">
                                Receipt {{ReceiptNumber}}
                              </h1>
                              <p style="margin: 12px 0 0; font-size: 16px; color: #64748B; line-height: 1.5;">
                                Thank you, <strong>{{ContributorName}}</strong>. We have received your contribution to {{OrganizationName}}.
                              </p>
                            </td>
                          </tr>

                          <!-- Amount Received Panel -->
                          <tr>
                            <td style="padding: 0 40px 40px;">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #F1F5F9; border-left: 5px solid #7C3AED; border-radius: 0 6px 6px 0;">
                                <tr>
                                  <td style="padding: 28px 32px;">
                                    <p style="margin: 0; font-size: 13px; font-weight: 700; color: #64748B; text-transform: uppercase; letter-spacing: 0.05em;">Amount Received</p>
                                    <h2 style="margin: 4px 0 0; font-size: 42px; font-weight: 800; color: #0F172A; letter-spacing: -0.04em;">{{Currency}} {{Amount}}</h2>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- Status Badge Strip -->
                          <tr>
                            <td style="padding: 0 40px 40px;">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background-color: #F0FDF4; border: 1px solid #BBF7D0; border-radius: 6px;">
                                <tr>
                                  <td style="padding: 14px 20px; color: #166534; font-size: 14px; font-weight: 600;">
                                    <span style="color: #22C55E; margin-right: 10px; font-size: 18px;">●</span> This contribution has been successfully processed and settled.
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- Detail Rows -->
                          <tr>
                            <td style="padding: 0 40px 48px;">
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0">
                                <tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Event</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{EventTitle}}</td>
                                </tr>
                                <tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Fund / Cause</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{FundName}}</td>
                                </tr>
                                <tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Reference</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{ContributionReference}}</td>
                                </tr>
                                <tr>
                                  <td style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; color: #64748B; font-weight: 500;">Issued At</td>
                                  <td align="right" style="padding: 16px 0; border-bottom: 1px solid #F1F5F9; font-size: 14px; font-weight: 700; color: #0F172A;">{{IssuedAt}}</td>
                                </tr>
                                <tr>
                                  <td style="padding: 16px 0; font-size: 14px; color: #64748B; font-weight: 500;">Receipt Number</td>
                                  <td align="right" style="padding: 16px 0; font-size: 14px; font-weight: 700; color: #0F172A;">{{ReceiptNumber}}</td>
                                </tr>
                              </table>
                            </td>
                          </tr>

                          <!-- Footer -->
                          <tr>
                            <td style="background-color: #F8FAFC; padding: 40px; border-top: 1px solid #F1F5F9;">
                              <p style="margin: 0; font-size: 13px; line-height: 1.6; color: #64748B;">
                                This is an automated receipt from <strong>{{OrganizationName}}</strong>. 
                                Please keep this email for your records. If you did not make this contribution 
                                or have any questions, please contact the organization directly.
                              </p>
                              <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="margin-top: 24px; border-top: 1px solid #E2E8F0; padding-top: 24px;">
                                <tr>
                                  <td style="font-size: 12px; color: #94A3B8; font-weight: 600; text-transform: uppercase; letter-spacing: 0.05em;">
                                    Support & Contact
                                  </td>
                                </tr>
                                <tr>
                                  <td style="padding-top: 8px;">
                                    <a href="mailto:support@mftlcollections.com" style="font-size: 14px; color: #7C3AED; text-decoration: none; font-weight: 600;">support@mftlcollections.com</a>
                                  </td>
                                </tr>
                              </table>
                            </td>
                          </tr>
                        </table>
                        
                        <!-- Bottom Disclaimer -->
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width: 600px; margin-top: 24px;">
                          <tr>
                            <td align="center" style="font-size: 11px; color: #94A3B8; line-height: 1.5;">
                              &copy; 2026 MFTL Collections. All rights reserved.<br>
                              Powered by MFTL Technology Stack.
                            </td>
                          </tr>
                        </table>
                      </td>
                    </tr>
                  </table>
                </body>
                </html>'
                WHERE "TemplateKey" = 'receipt.issued' AND "Channel" = 'Email';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
                WHERE "TemplateKey" = 'receipt.issued' AND "Channel" = 'Email';
                """);
        }
    }
}
