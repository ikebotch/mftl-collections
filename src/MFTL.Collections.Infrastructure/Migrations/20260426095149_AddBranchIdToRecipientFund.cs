using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchIdToRecipientFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "RecipientFunds",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecipientFunds_BranchId",
                table: "RecipientFunds",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_RecipientFunds_Branches_BranchId",
                table: "RecipientFunds",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_RecipientFunds_Branches_BranchId",
                table: "RecipientFunds");

            migrationBuilder.DropIndex(
                name: "IX_RecipientFunds_BranchId",
                table: "RecipientFunds");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "RecipientFunds");
        }
    }
}
