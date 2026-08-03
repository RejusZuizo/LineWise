using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Linewise.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ImportTemplatesAndCommittedImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommittedImports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ImportTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    ImportedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ImportedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    FirstDate = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    EndDateExclusive = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    RowsImported = table.Column<int>(type: "INTEGER", nullable: false),
                    RowsResolvedManually = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommittedImports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    WorksheetName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    HeaderRowIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    NameColumnIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstDateColumnIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    DateFormat = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    EmptyCellStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportUnrecognisedCells = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportFiles",
                columns: table => new
                {
                    CommittedImportId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Content = table.Column<byte[]>(type: "BLOB", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportFiles", x => x.CommittedImportId);
                    table.ForeignKey(
                        name: "FK_ImportFiles_CommittedImports_CommittedImportId",
                        column: x => x.CommittedImportId,
                        principalTable: "CommittedImports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportTemplateRules",
                columns: table => new
                {
                    ImportTemplateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Ordinal = table.Column<int>(type: "INTEGER", nullable: false),
                    Kind = table.Column<int>(type: "INTEGER", nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportTemplateRules", x => new { x.ImportTemplateId, x.Ordinal });
                    table.ForeignKey(
                        name: "FK_ImportTemplateRules_ImportTemplates_ImportTemplateId",
                        column: x => x.ImportTemplateId,
                        principalTable: "ImportTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommittedImports_ImportedAtUtc",
                table: "CommittedImports",
                column: "ImportedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportFiles");

            migrationBuilder.DropTable(
                name: "ImportTemplateRules");

            migrationBuilder.DropTable(
                name: "CommittedImports");

            migrationBuilder.DropTable(
                name: "ImportTemplates");
        }
    }
}
