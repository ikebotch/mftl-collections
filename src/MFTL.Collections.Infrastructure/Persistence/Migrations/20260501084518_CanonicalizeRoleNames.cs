using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CanonicalizeRoleNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update UserScopeAssignments
            migrationBuilder.Sql(@"
                UPDATE ""UserScopeAssignments"" 
                SET ""Role"" = CASE 
                    WHEN ""Role"" IN ('Organisation Admin', 'Organization Admin', 'Org Admin', 'Tenant Admin', 'OrganisationAdmin', 'OrganizationAdmin', 'OrgAdmin', 'TenantAdmin', 'Tenant Owner') THEN 'OrganisationAdmin'
                    WHEN ""Role"" IN ('Platform Admin', 'PlatformAdmin') THEN 'PlatformAdmin'
                    WHEN ""Role"" IN ('Branch Admin', 'BranchAdmin') THEN 'BranchAdmin'
                    WHEN ""Role"" IN ('Finance Admin', 'FinanceAdmin') THEN 'FinanceAdmin'
                    WHEN ""Role"" IN ('Accountant') THEN 'FinanceAdmin'
                    WHEN ""Role"" IN ('Event Manager', 'EventManager') THEN 'EventManager'
                    WHEN ""Role"" IN ('Notification Manager', 'NotificationManager') THEN 'NotificationManager'
                    WHEN ""Role"" IN ('Collector') THEN 'Collector'
                    WHEN ""Role"" IN ('Viewer') THEN 'Viewer'
                    ELSE ""Role""
                END;");

            // Update RolePermissions
            migrationBuilder.Sql(@"
                UPDATE ""RolePermissions"" 
                SET ""RoleName"" = CASE 
                    WHEN ""RoleName"" IN ('Organisation Admin', 'Organization Admin', 'Org Admin', 'Tenant Admin', 'OrganisationAdmin', 'OrganizationAdmin', 'OrgAdmin', 'TenantAdmin', 'Tenant Owner') THEN 'OrganisationAdmin'
                    WHEN ""RoleName"" IN ('Platform Admin', 'PlatformAdmin') THEN 'PlatformAdmin'
                    WHEN ""RoleName"" IN ('Branch Admin', 'BranchAdmin') THEN 'BranchAdmin'
                    WHEN ""RoleName"" IN ('Finance Admin', 'FinanceAdmin') THEN 'FinanceAdmin'
                    WHEN ""RoleName"" IN ('Accountant') THEN 'FinanceAdmin'
                    WHEN ""RoleName"" IN ('Event Manager', 'EventManager') THEN 'EventManager'
                    WHEN ""RoleName"" IN ('Notification Manager', 'NotificationManager') THEN 'NotificationManager'
                    WHEN ""RoleName"" IN ('Collector') THEN 'Collector'
                    WHEN ""RoleName"" IN ('Viewer') THEN 'Viewer'
                    ELSE ""RoleName""
                END;");

            // Remove duplicates from RolePermissions (now that role names are merged)
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions"" a USING (
                    SELECT MIN(""Id""::text) as keep_id_text, ""RoleName"", ""PermissionKey""
                    FROM ""RolePermissions""
                    GROUP BY ""RoleName"", ""PermissionKey""
                    HAVING COUNT(*) > 1
                ) b
                WHERE a.""RoleName"" = b.""RoleName"" 
                  AND a.""PermissionKey"" = b.""PermissionKey""
                  AND a.""Id""::text <> b.keep_id_text;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
