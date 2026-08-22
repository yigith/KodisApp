using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodisApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotebookUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    EmailVerified = table.Column<bool>(type: "INTEGER", nullable: false),
                    UserName = table.Column<string>(type: "TEXT", nullable: true),
                    Sub = table.Column<string>(type: "TEXT", nullable: true),
                    LoginMethod = table.Column<int>(type: "INTEGER", nullable: false),
                    Picture = table.Column<string>(type: "TEXT", nullable: true),
                    FullName = table.Column<string>(type: "TEXT", nullable: true),
                    GivenName = table.Column<string>(type: "TEXT", nullable: true),
                    FamilyName = table.Column<string>(type: "TEXT", nullable: true),
                    Locale = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    LastLoginDate = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotebookUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoginSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    NotebookUserId = table.Column<string>(type: "TEXT", nullable: false),
                    RefreshTokenId = table.Column<string>(type: "TEXT", nullable: false),
                    Expires = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    RefreshedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    RevokedDate = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginSessions_NotebookUsers_NotebookUserId",
                        column: x => x.NotebookUserId,
                        principalTable: "NotebookUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Notebooks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Slug = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    CreatedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    ExpireDate = table.Column<long>(type: "INTEGER", nullable: false),
                    SecurityToken = table.Column<string>(type: "TEXT", nullable: false),
                    IsDeleted = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMain = table.Column<bool>(type: "INTEGER", nullable: false),
                    PasswordSalt = table.Column<string>(type: "TEXT", nullable: true),
                    ViewPasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    EditPasswordHash = table.Column<string>(type: "TEXT", nullable: true),
                    NotebookUserId = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notebooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notebooks_NotebookUsers_NotebookUserId",
                        column: x => x.NotebookUserId,
                        principalTable: "NotebookUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Notes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    NotebookId = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    ModifiedDate = table.Column<long>(type: "INTEGER", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notes_Notebooks_NotebookId",
                        column: x => x.NotebookId,
                        principalTable: "Notebooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginSessions_Expires",
                table: "LoginSessions",
                column: "Expires");

            migrationBuilder.CreateIndex(
                name: "IX_LoginSessions_NotebookUserId",
                table: "LoginSessions",
                column: "NotebookUserId");

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
                name: "IX_Notes_NotebookId",
                table: "Notes",
                column: "NotebookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginSessions");

            migrationBuilder.DropTable(
                name: "Notes");

            migrationBuilder.DropTable(
                name: "Notebooks");

            migrationBuilder.DropTable(
                name: "NotebookUsers");
        }
    }
}
