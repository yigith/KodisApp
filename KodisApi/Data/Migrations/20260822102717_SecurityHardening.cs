using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodisApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class SecurityHardening : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // --- Normalise existing rows so the new unique indexes can be built ---

            // Empty strings were the old default for "not set"; the filtered
            // unique indexes below key off NULL instead, and several ''
            // values would collide with each other.
            migrationBuilder.Sql(
                "UPDATE \"NotebookUsers\" SET \"UserName\" = NULL WHERE trim(\"UserName\") = '';");
            migrationBuilder.Sql(
                "UPDATE \"NotebookUsers\" SET \"Sub\" = NULL WHERE trim(\"Sub\") = '';");

            // Notebooks whose slug was never filled in (the second SaveChanges
            // of the old two-step create failed) are unreachable over the API
            // but would collide on the new unique slug index. Give them a
            // distinct placeholder rather than deleting the rows.
            migrationBuilder.Sql(
                "UPDATE \"Notebooks\" SET \"Slug\" = 'orphan-' || \"Id\" WHERE trim(\"Slug\") = '';");

            // Keep only the newest main notebook per user; the others lose the
            // flag so the new filtered unique index holds. No data is removed.
            migrationBuilder.Sql(@"
                UPDATE ""Notebooks"" SET ""IsMain"" = FALSE
                WHERE ""IsMain"" = TRUE AND ""Id"" NOT IN (
                    SELECT MAX(""Id"") FROM ""Notebooks""
                    WHERE ""IsMain"" = TRUE AND ""NotebookUserId"" IS NOT NULL
                    GROUP BY ""NotebookUserId"");");

            // The JWT signing key is rotated as part of this change, so every
            // token already in circulation is void anyway. Dropping the sessions
            // is what lets RefreshTokenId become a non-nullable column without
            // inventing values for rows that can never be used again.
            migrationBuilder.Sql("DELETE FROM \"LoginSessions\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Notebooks_NotebookUsers_NotebookUserId",
                table: "Notebooks");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_NotebookUserId",
                table: "Notebooks");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Notebooks",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "Expires",
                table: "LoginSessions",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.AddColumn<string>(
                name: "RefreshTokenId",
                table: "LoginSessions",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedDate",
                table: "LoginSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotebookUsers_Email",
                table: "NotebookUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotebookUsers_LoginMethod_Sub",
                table: "NotebookUsers",
                columns: new[] { "LoginMethod", "Sub" },
                unique: true,
                filter: "\"Sub\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_NotebookUsers_UserName",
                table: "NotebookUsers",
                column: "UserName",
                unique: true,
                filter: "\"UserName\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_ExpireDate",
                table: "Notebooks",
                column: "ExpireDate");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_NotebookUserId_Main",
                table: "Notebooks",
                column: "NotebookUserId",
                unique: true,
                filter: "\"IsMain\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_Slug",
                table: "Notebooks",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginSessions_Expires",
                table: "LoginSessions",
                column: "Expires");

            migrationBuilder.AddForeignKey(
                name: "FK_Notebooks_NotebookUsers_NotebookUserId",
                table: "Notebooks",
                column: "NotebookUserId",
                principalTable: "NotebookUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notebooks_NotebookUsers_NotebookUserId",
                table: "Notebooks");

            migrationBuilder.DropIndex(
                name: "IX_NotebookUsers_Email",
                table: "NotebookUsers");

            migrationBuilder.DropIndex(
                name: "IX_NotebookUsers_LoginMethod_Sub",
                table: "NotebookUsers");

            migrationBuilder.DropIndex(
                name: "IX_NotebookUsers_UserName",
                table: "NotebookUsers");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_ExpireDate",
                table: "Notebooks");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_NotebookUserId_Main",
                table: "Notebooks");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_Slug",
                table: "Notebooks");

            migrationBuilder.DropIndex(
                name: "IX_LoginSessions_Expires",
                table: "LoginSessions");

            migrationBuilder.DropColumn(
                name: "RefreshTokenId",
                table: "LoginSessions");

            migrationBuilder.DropColumn(
                name: "RevokedDate",
                table: "LoginSessions");

            migrationBuilder.AlterColumn<string>(
                name: "Slug",
                table: "Notebooks",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<DateTime>(
                name: "Expires",
                table: "LoginSessions",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.CreateIndex(
                name: "IX_Notebooks_NotebookUserId",
                table: "Notebooks",
                column: "NotebookUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Notebooks_NotebookUsers_NotebookUserId",
                table: "Notebooks",
                column: "NotebookUserId",
                principalTable: "NotebookUsers",
                principalColumn: "Id");
        }
    }
}
