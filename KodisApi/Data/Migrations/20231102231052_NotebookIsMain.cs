using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KodisApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class NotebookIsMain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMain",
                table: "Notebooks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsMain",
                table: "Notebooks");
        }
    }
}
