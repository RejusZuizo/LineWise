using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Linewise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ClosedLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsClosed",
                table: "LineDemands",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsClosed",
                table: "LineDemands");
        }
    }
}
