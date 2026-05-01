using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnsureCanonicalRolePermissionsUnique : Migration
    {
        /// <inheritdoc />
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Canonicalize RoleName in RolePermissions (incase previous migration missed some or new ones were added)
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

            // 2. Remove duplicates before adding unique index
            migrationBuilder.Sql(@"
                DELETE FROM ""RolePermissions"" a USING (
                    SELECT MIN(""Id""::text)::uuid as keep_id, ""RoleName"", ""PermissionKey""
                    FROM ""RolePermissions""
                    GROUP BY ""RoleName"", ""PermissionKey""
                    HAVING COUNT(*) > 1
                ) b
                WHERE a.""RoleName"" = b.""RoleName"" 
                  AND a.""PermissionKey"" = b.""PermissionKey""
                  AND a.""Id"" <> b.keep_id;");

            // 3. Create Unique Index
            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleName_PermissionKey",
                table: "RolePermissions",
                columns: new[] { "RoleName", "PermissionKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RolePermissions_RoleName_PermissionKey",
                table: "RolePermissions");
        }
    }
}
