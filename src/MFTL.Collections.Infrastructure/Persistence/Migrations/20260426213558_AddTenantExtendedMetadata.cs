using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantExtendedMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultCurrency",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultLocale",
                table: "Tenants",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MissionStatement",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PosLogoUrl",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrimaryLogoUrl",
                table: "Tenants",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                table: "Tenants",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCurrency",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "DefaultLocale",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MissionStatement",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PosLogoUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PrimaryLogoUrl",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "SupportEmail",
                table: "Tenants");
        }
    }
}
