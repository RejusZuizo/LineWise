using System.IO.Compression;
using ClosedXML.Excel;

namespace Linewise.Tests.Builders;

/// <summary>
/// Builds spreadsheets in memory for the reader to chew on.
/// </summary>
/// <remarks>
/// Generated rather than committed. A sheet from the factory is a list of who works where,
/// which is personal data, and it does not belong in a repository under any circumstances.
/// Generating them also means a fixture cannot silently drift from what the test claims it
/// contains.
/// </remarks>
internal static class WorkbookFixture
{
    public static MemoryStream WrittenMarks(string sheetName = "Availability")
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);

        sheet.Cell(1, 1).Value = "Name";

        for (var day = 0; day < 5; day++)
        {
            sheet.Cell(1, 2 + day).Value = SheetBuilder.Monday.AddDays(day).ToDateTime(TimeOnly.MinValue);
            sheet.Cell(1, 2 + day).Style.DateFormat.Format = "yyyy-mm-dd";
        }

        Write(sheet, 2, "Ada Fictional", "work", "work", "holiday", "work", "work");
        Write(sheet, 3, "  Bram Invented  ", "work", "work", "work", string.Empty, "ot");
        Write(sheet, 4, "Notreal, Cleo", "work", "holiday", "work", "work", "work");
        Write(sheet, 5, "Agency Person", "work", "work", "work", "work", "work");

        return Save(workbook);
    }

    /// <summary>
    /// Cells coloured from the theme palette, which is what the standard Excel colour picker
    /// produces and therefore what most real sheets actually contain.
    /// </summary>
    public static MemoryStream ThemeColouredCells()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Availability");

        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = SheetBuilder.Monday.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(1, 2).Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.Cell(2, 1).Value = "Ada Fictional";
        sheet.Cell(2, 2).Style.Fill.SetBackgroundColor(XLColor.FromTheme(XLThemeColor.Accent1));

        sheet.Cell(3, 1).Value = "Bram Invented";
        sheet.Cell(3, 2).Style.Fill.SetBackgroundColor(XLColor.FromTheme(XLThemeColor.Accent2, 0.4));

        return Save(workbook);
    }

    public static MemoryStream PlainColouredCells()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Availability");

        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = SheetBuilder.Monday.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(1, 2).Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.Cell(2, 1).Value = "Ada Fictional";
        sheet.Cell(2, 2).Style.Fill.SetBackgroundColor(XLColor.FromArgb(0x00, 0xB0, 0x50));

        sheet.Cell(3, 1).Value = "Bram Invented";

        return Save(workbook);
    }

    /// <summary>A cell holding a formula, which must never be evaluated during an import.</summary>
    public static MemoryStream WithAFormula()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Availability");

        sheet.Cell(1, 1).Value = "Name";
        sheet.Cell(1, 2).Value = SheetBuilder.Monday.ToDateTime(TimeOnly.MinValue);
        sheet.Cell(1, 2).Style.DateFormat.Format = "yyyy-mm-dd";

        sheet.Cell(2, 1).Value = "Ada Fictional";
        sheet.Cell(2, 2).FormulaA1 = "=CONCATENATE(\"wo\",\"rk\")";

        return Save(workbook);
    }

    /// <summary>A plain list of people, as a site would supply for first-time setup.</summary>
    public static MemoryStream EmployeeList()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Employees");

        sheet.Cell(1, 1).Value = "Full name";
        sheet.Cell(1, 2).Value = "Also known as";

        sheet.Cell(2, 1).Value = "Ada Fictional";
        sheet.Cell(3, 1).Value = "Fictional, Ada";
        sheet.Cell(4, 1).Value = "Dara Madeup";
        sheet.Cell(4, 2).Value = "D Madeup; Dara M";
        sheet.Cell(5, 1).Value = "Eli Pretend";

        return Save(workbook);
    }

    public static MemoryStream NotASpreadsheet() =>
        new(System.Text.Encoding.UTF8.GetBytes("This is a text file wearing an xlsx extension."));

    /// <summary>A perfectly valid zip archive that simply is not a workbook.</summary>
    public static MemoryStream ZipWithoutAWorkbook()
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry("readme.txt").Open();
            entry.Write("nothing to see"u8);
        }

        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// A small archive whose contents expand enormously. The real thing nests this many
    /// layers deep; one layer is enough to prove the ratio check bites.
    /// </summary>
    public static MemoryStream DecompressionBomb()
    {
        var stream = new MemoryStream();

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry("[Content_Types].xml").Open();
            entry.Write(new byte[32 * 1024 * 1024]);
        }

        stream.Position = 0;
        return stream;
    }

    private static void Write(IXLWorksheet sheet, int row, string name, params string[] marks)
    {
        sheet.Cell(row, 1).Value = name;

        for (var index = 0; index < marks.Length; index++)
        {
            if (marks[index].Length > 0)
            {
                sheet.Cell(row, 2 + index).Value = marks[index];
            }
        }
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return stream;
    }
}
