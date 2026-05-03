using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentFailureReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "Payments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "Payments");
        }
    }
}
