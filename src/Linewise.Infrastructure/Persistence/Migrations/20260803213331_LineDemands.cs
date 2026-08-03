using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Linewise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LineDemands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LineDemands",
                columns: table => new
                {
                    LineId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Date = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RequiredHeadcount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LineDemands", x => new { x.LineId, x.Date });
                    table.ForeignKey(
                        name: "FK_LineDemands_Lines_LineId",
                        column: x => x.LineId,
                        principalTable: "Lines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LineDemands_Date",
                table: "LineDemands",
                column: "Date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LineDemands");
        }
    }
}
