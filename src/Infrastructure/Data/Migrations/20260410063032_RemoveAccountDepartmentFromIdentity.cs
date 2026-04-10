using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveAccountDepartmentFromIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_department_id_department",
                table: "account");

            migrationBuilder.DropIndex(
                name: "IX_account_id_department",
                table: "account");

            migrationBuilder.DropColumn(
                name: "id_department",
                table: "account");

            migrationBuilder.AlterColumn<Guid>(
                name: "membership_id",
                table: "refresh_token",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM refresh_token
                WHERE membership_id IS NULL;
                """);

            migrationBuilder.AlterColumn<Guid>(
                name: "membership_id",
                table: "refresh_token",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "id_department",
                table: "account",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_account_id_department",
                table: "account",
                column: "id_department");

            migrationBuilder.AddForeignKey(
                name: "FK_account_department_id_department",
                table: "account",
                column: "id_department",
                principalTable: "department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
