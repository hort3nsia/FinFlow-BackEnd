using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinFlow.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplyCleanArchitectureSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ACCOUNT_DEPARTMENT_ID_DEPARTMENT",
                table: "ACCOUNT");

            migrationBuilder.DropForeignKey(
                name: "FK_ACCOUNT_TENANT_ID_TENANT",
                table: "ACCOUNT");

            migrationBuilder.DropForeignKey(
                name: "FK_DEPARTMENT_DEPARTMENT_PARENT_ID",
                table: "DEPARTMENT");

            migrationBuilder.DropForeignKey(
                name: "FK_DEPARTMENT_TENANT_ID_TENANT",
                table: "DEPARTMENT");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TENANT",
                table: "TENANT");

            migrationBuilder.DropPrimaryKey(
                name: "PK_DEPARTMENT",
                table: "DEPARTMENT");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ACCOUNT",
                table: "ACCOUNT");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "TENANT");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "DEPARTMENT");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "DEPARTMENT");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "DEPARTMENT");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "DEPARTMENT");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "ACCOUNT");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "ACCOUNT");

            migrationBuilder.DropColumn(
                name: "LastModifiedAt",
                table: "ACCOUNT");

            migrationBuilder.DropColumn(
                name: "LastModifiedBy",
                table: "ACCOUNT");

            migrationBuilder.RenameTable(
                name: "TENANT",
                newName: "tenant");

            migrationBuilder.RenameTable(
                name: "DEPARTMENT",
                newName: "department");

            migrationBuilder.RenameTable(
                name: "ACCOUNT",
                newName: "account");

            migrationBuilder.RenameColumn(
                name: "TENANT_CODE",
                table: "tenant",
                newName: "tenant_code");

            migrationBuilder.RenameColumn(
                name: "TENANCY_MODEL",
                table: "tenant",
                newName: "tenancy_model");

            migrationBuilder.RenameColumn(
                name: "NAME",
                table: "tenant",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "CURRENCY",
                table: "tenant",
                newName: "currency");

            migrationBuilder.RenameColumn(
                name: "CONNECTION_STRING",
                table: "tenant",
                newName: "connection_string");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "tenant",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_TENANT_TENANT_CODE",
                table: "tenant",
                newName: "IX_tenant_tenant_code");

            migrationBuilder.RenameColumn(
                name: "PARENT_ID",
                table: "department",
                newName: "parent_id");

            migrationBuilder.RenameColumn(
                name: "NAME",
                table: "department",
                newName: "name");

            migrationBuilder.RenameColumn(
                name: "ID_TENANT",
                table: "department",
                newName: "id_tenant");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "department",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_DEPARTMENT_PARENT_ID",
                table: "department",
                newName: "IX_department_parent_id");

            migrationBuilder.RenameIndex(
                name: "IX_DEPARTMENT_ID_TENANT",
                table: "department",
                newName: "IX_department_id_tenant");

            migrationBuilder.RenameColumn(
                name: "ROLE",
                table: "account",
                newName: "role");

            migrationBuilder.RenameColumn(
                name: "PASSWORD_HASH",
                table: "account",
                newName: "password_hash");

            migrationBuilder.RenameColumn(
                name: "ID_TENANT",
                table: "account",
                newName: "id_tenant");

            migrationBuilder.RenameColumn(
                name: "ID_DEPARTMENT",
                table: "account",
                newName: "id_department");

            migrationBuilder.RenameColumn(
                name: "EMAIL",
                table: "account",
                newName: "email");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "account",
                newName: "created_at");

            migrationBuilder.RenameIndex(
                name: "IX_ACCOUNT_ID_TENANT",
                table: "account",
                newName: "IX_account_id_tenant");

            migrationBuilder.RenameIndex(
                name: "IX_ACCOUNT_ID_DEPARTMENT",
                table: "account",
                newName: "IX_account_id_department");

            migrationBuilder.RenameIndex(
                name: "IX_ACCOUNT_EMAIL",
                table: "account",
                newName: "IX_account_email");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "tenant",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "department",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "account",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_tenant",
                table: "tenant",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_department",
                table: "department",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_account",
                table: "account",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_account_department_id_department",
                table: "account",
                column: "id_department",
                principalTable: "department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_account_tenant_id_tenant",
                table: "account",
                column: "id_tenant",
                principalTable: "tenant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_department_department_parent_id",
                table: "department",
                column: "parent_id",
                principalTable: "department",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_department_tenant_id_tenant",
                table: "department",
                column: "id_tenant",
                principalTable: "tenant",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_account_department_id_department",
                table: "account");

            migrationBuilder.DropForeignKey(
                name: "FK_account_tenant_id_tenant",
                table: "account");

            migrationBuilder.DropForeignKey(
                name: "FK_department_department_parent_id",
                table: "department");

            migrationBuilder.DropForeignKey(
                name: "FK_department_tenant_id_tenant",
                table: "department");

            migrationBuilder.DropPrimaryKey(
                name: "PK_tenant",
                table: "tenant");

            migrationBuilder.DropPrimaryKey(
                name: "PK_department",
                table: "department");

            migrationBuilder.DropPrimaryKey(
                name: "PK_account",
                table: "account");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "tenant");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "department");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "account");

            migrationBuilder.RenameTable(
                name: "tenant",
                newName: "TENANT");

            migrationBuilder.RenameTable(
                name: "department",
                newName: "DEPARTMENT");

            migrationBuilder.RenameTable(
                name: "account",
                newName: "ACCOUNT");

            migrationBuilder.RenameColumn(
                name: "tenant_code",
                table: "TENANT",
                newName: "TENANT_CODE");

            migrationBuilder.RenameColumn(
                name: "tenancy_model",
                table: "TENANT",
                newName: "TENANCY_MODEL");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "TENANT",
                newName: "NAME");

            migrationBuilder.RenameColumn(
                name: "currency",
                table: "TENANT",
                newName: "CURRENCY");

            migrationBuilder.RenameColumn(
                name: "connection_string",
                table: "TENANT",
                newName: "CONNECTION_STRING");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "TENANT",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_tenant_tenant_code",
                table: "TENANT",
                newName: "IX_TENANT_TENANT_CODE");

            migrationBuilder.RenameColumn(
                name: "parent_id",
                table: "DEPARTMENT",
                newName: "PARENT_ID");

            migrationBuilder.RenameColumn(
                name: "name",
                table: "DEPARTMENT",
                newName: "NAME");

            migrationBuilder.RenameColumn(
                name: "id_tenant",
                table: "DEPARTMENT",
                newName: "ID_TENANT");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "DEPARTMENT",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_department_parent_id",
                table: "DEPARTMENT",
                newName: "IX_DEPARTMENT_PARENT_ID");

            migrationBuilder.RenameIndex(
                name: "IX_department_id_tenant",
                table: "DEPARTMENT",
                newName: "IX_DEPARTMENT_ID_TENANT");

            migrationBuilder.RenameColumn(
                name: "role",
                table: "ACCOUNT",
                newName: "ROLE");

            migrationBuilder.RenameColumn(
                name: "password_hash",
                table: "ACCOUNT",
                newName: "PASSWORD_HASH");

            migrationBuilder.RenameColumn(
                name: "id_tenant",
                table: "ACCOUNT",
                newName: "ID_TENANT");

            migrationBuilder.RenameColumn(
                name: "id_department",
                table: "ACCOUNT",
                newName: "ID_DEPARTMENT");

            migrationBuilder.RenameColumn(
                name: "email",
                table: "ACCOUNT",
                newName: "EMAIL");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "ACCOUNT",
                newName: "CreatedAt");

            migrationBuilder.RenameIndex(
                name: "IX_account_id_tenant",
                table: "ACCOUNT",
                newName: "IX_ACCOUNT_ID_TENANT");

            migrationBuilder.RenameIndex(
                name: "IX_account_id_department",
                table: "ACCOUNT",
                newName: "IX_ACCOUNT_ID_DEPARTMENT");

            migrationBuilder.RenameIndex(
                name: "IX_account_email",
                table: "ACCOUNT",
                newName: "IX_ACCOUNT_EMAIL");

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "TENANT",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "TENANT",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "TENANT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "DEPARTMENT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "DEPARTMENT",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "DEPARTMENT",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "DEPARTMENT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "ACCOUNT",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "ACCOUNT",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastModifiedAt",
                table: "ACCOUNT",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastModifiedBy",
                table: "ACCOUNT",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_TENANT",
                table: "TENANT",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_DEPARTMENT",
                table: "DEPARTMENT",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ACCOUNT",
                table: "ACCOUNT",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ACCOUNT_DEPARTMENT_ID_DEPARTMENT",
                table: "ACCOUNT",
                column: "ID_DEPARTMENT",
                principalTable: "DEPARTMENT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ACCOUNT_TENANT_ID_TENANT",
                table: "ACCOUNT",
                column: "ID_TENANT",
                principalTable: "TENANT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DEPARTMENT_DEPARTMENT_PARENT_ID",
                table: "DEPARTMENT",
                column: "PARENT_ID",
                principalTable: "DEPARTMENT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DEPARTMENT_TENANT_ID_TENANT",
                table: "DEPARTMENT",
                column: "ID_TENANT",
                principalTable: "TENANT",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
