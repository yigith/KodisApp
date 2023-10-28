using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodisApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotebookUsersAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrivate",
                table: "Notes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NotebookUserId",
                table: "Notebooks",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NotebookUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    UserName = table.Column<string>(type: "text", nullable: true),
                    Sub = table.Column<string>(type: "text", nullable: true),
                    LoginMethod = table.Column<int>(type: "integer", nullable: false),
                    Picture = table.Column<string>(type: "text", nullable: true),
                    FullName = table.Column<string>(type: "text", nullable: true),
                    GivenName = table.Column<string>(type: "text", nullable: true),
                    FamilyName = table.Column<string>(type: "text", nullable: true),
                    Locale = table.Column<string>(type: "text", nullable: true),
                    CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastLoginDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookUsers", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Notebooks_NotebookUsers_NotebookUserId",
                table: "Notebooks");

            migrationBuilder.DropTable(
                name: "NotebookUsers");

            migrationBuilder.DropIndex(
                name: "IX_Notebooks_NotebookUserId",
                table: "Notebooks");

            migrationBuilder.DropColumn(
                name: "IsPrivate",
                table: "Notes");

            migrationBuilder.DropColumn(
                name: "NotebookUserId",
                table: "Notebooks");
        }
    }
}
