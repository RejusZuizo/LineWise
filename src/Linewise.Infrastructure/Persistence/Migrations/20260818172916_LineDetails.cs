using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Linewise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LineDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Lines",
                type: "TEXT",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LayoutNotes",
                table: "Lines",
                type: "TEXT",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Description",
                table: "Lines");

            migrationBuilder.DropColumn(
                name: "LayoutNotes",
                table: "Lines");
        }
    }
}
