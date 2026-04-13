using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailVerificationAndChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "email_verified_at",
                table: "account",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_email_verified",
                table: "account",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "email_challenge",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<int>(type: "integer", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    otp_hash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    max_otp_attempts = table.Column<int>(type: "integer", nullable: false, defaultValue: 5),
                    otp_attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_challenge", x => x.Id);
                    table.ForeignKey(
                        name: "FK_email_challenge_account_account_id",
                        column: x => x.account_id,
                        principalTable: "account",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_email_challenge_account_id",
                table: "email_challenge",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_email_challenge_account_id_purpose",
                table: "email_challenge",
                columns: new[] { "account_id", "purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_email_challenge_email",
                table: "email_challenge",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_email_challenge_token_hash",
                table: "email_challenge",
                column: "token_hash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_challenge");

            migrationBuilder.DropColumn(
                name: "email_verified_at",
                table: "account");

            migrationBuilder.DropColumn(
                name: "is_email_verified",
                table: "account");
        }
    }
}
