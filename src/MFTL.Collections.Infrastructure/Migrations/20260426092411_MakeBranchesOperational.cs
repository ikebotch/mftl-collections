using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MFTL.Collections.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeBranchesOperational : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Settlements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Receipts",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Contributors",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "Contributions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settlements_BranchId",
                table: "Settlements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Receipts_BranchId",
                table: "Receipts",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Events_BranchId",
                table: "Events",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributors_BranchId",
                table: "Contributors",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Contributions_BranchId",
                table: "Contributions",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_Branches_BranchId",
                table: "Contributions",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributors_Branches_BranchId",
                table: "Contributors",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Events_Branches_BranchId",
                table: "Events",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Receipts_Branches_BranchId",
                table: "Receipts",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Settlements_Branches_BranchId",
                table: "Settlements",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_Branches_BranchId",
                table: "Contributions");

            migrationBuilder.DropForeignKey(
                name: "FK_Contributors_Branches_BranchId",
                table: "Contributors");

            migrationBuilder.DropForeignKey(
                name: "FK_Events_Branches_BranchId",
                table: "Events");

            migrationBuilder.DropForeignKey(
                name: "FK_Receipts_Branches_BranchId",
                table: "Receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_Settlements_Branches_BranchId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Settlements_BranchId",
                table: "Settlements");

            migrationBuilder.DropIndex(
                name: "IX_Receipts_BranchId",
                table: "Receipts");

            migrationBuilder.DropIndex(
                name: "IX_Events_BranchId",
                table: "Events");

            migrationBuilder.DropIndex(
                name: "IX_Contributors_BranchId",
                table: "Contributors");

            migrationBuilder.DropIndex(
                name: "IX_Contributions_BranchId",
                table: "Contributions");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Settlements");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Events");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Contributors");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Contributions");
        }
    }
}
