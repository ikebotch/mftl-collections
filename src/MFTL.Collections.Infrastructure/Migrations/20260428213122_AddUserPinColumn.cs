using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPinColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Pin",
                table: "Users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pin",
                table: "Users");
        }
    }
}
