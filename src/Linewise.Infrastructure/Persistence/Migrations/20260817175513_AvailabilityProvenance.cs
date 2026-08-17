using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Linewise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AvailabilityProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Source",
                table: "Availabilities",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Availabilities_Date_Source",
                table: "Availabilities",
                columns: new[] { "Date", "Source" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Availabilities_Date_Source",
                table: "Availabilities");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "Availabilities");
        }
    }
}
