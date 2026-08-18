using System.Globalization;
using Linewise.Application.Printing;
using Linewise.Application.Rostering;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Linewise.Domain.Rostering;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Linewise.Infrastructure.Printing;

/// <inheritdoc cref="IRosterPrinter"/>
public sealed class QuestPdfRosterPrinter : IRosterPrinter
{
    public Task<byte[]> PrintFullSheetAsync(
        PrintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var names = new NameBook(request);
        var present = new Attendance(request.Availabilities);

        return Task.FromResult(Document.Create(document =>
        {
            // One page per date and shift. A day per page is what lets the type be large
            // enough to read from a distance.
            foreach (var day in request.Roster.Days)
            {
                document.Page(page =>
                {
                    Frame(page, request, names.ShiftTitle(day));
                    page.Content().Element(content => FullSheetBody(content, request, names, day, present));
                });
            }
        }).GeneratePdf());
    }

    public Task<byte[]> PrintPerLineSheetsAsync(
        PrintRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var names = new NameBook(request);
        var present = new Attendance(request.Availabilities);

        return Task.FromResult(Document.Create(document =>
        {
            // One page per line, to pin at that line. A worker at Ovens does not need to
            // read about Packing, and giving them the whole factory makes them read none of it.
            foreach (var line in request.Lines.OrderBy(line => line.DisplayOrder))
            {
                document.Page(page =>
                {
                    Frame(page, request, line.Name);
                    page.Content().Element(content => PerLineBody(content, request, names, line, present));
                });
            }
        }).GeneratePdf());
    }

    public Task<byte[]> PrintAmendmentSlipAsync(
        AmendmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var changes = RosterDiff.Between(request.Previous, request.Current.Roster);
        var names = new NameBook(request.Current);

        return Task.FromResult(Document.Create(document =>
        {
            document.Page(page =>
            {
                Frame(page, request.Current, "AMENDMENT", explainsTheLeaderMark: false);
                page.Content().Element(content => AmendmentBody(content, request.Current, names, changes));
            });
        }).GeneratePdf());
    }

    /// <summary>
    /// The page furniture every layout shares: who, what, when, and which version. The
    /// version number is the point of the header. A sheet on a wall gives no clue that it has
    /// been superseded unless it carries a number somebody can compare.
    /// </summary>
    /// <param name="explainsTheLeaderMark">
    /// Whether the footer should explain the leader mark. False on the amendment slip, which
    /// carries no marks: a legend for something that is not on the page is one more thing to
    /// read and discard.
    /// </param>
    private static void Frame(
        PageDescriptor page,
        PrintRequest request,
        string subtitle,
        bool explainsTheLeaderMark = true)
    {
        var settings = request.Settings;

        page.Size(RosterDocumentStyle.PageSizeFor(settings));
        page.Margin(settings.BaseFontPoints);
        page.DefaultTextStyle(text => text
            .FontSize(RosterDocumentStyle.Body(settings))
            .FontColor(RosterDocumentStyle.Ink));

        page.Header().Element(header => header
            .PaddingBottom(settings.BaseFontPoints * 0.4f)
            .BorderBottom(2)
            .BorderColor(RosterDocumentStyle.Ink)
            .Row(row =>
            {
                if (settings.LogoPng is { Length: > 0 } logo)
                {
                    row.ConstantItem(settings.BaseFontPoints * 4).Image(logo);
                    row.ConstantItem(settings.BaseFontPoints);
                }

                row.RelativeItem().Column(column =>
                {
                    if (!string.IsNullOrWhiteSpace(settings.CompanyName))
                    {
                        column.Item().Text(settings.CompanyName)
                            .FontSize(RosterDocumentStyle.Small(settings))
                            .FontColor(RosterDocumentStyle.Muted);
                    }

                    column.Item().Text(subtitle)
                        .FontSize(RosterDocumentStyle.Title(settings))
                        .Bold();
                });

                row.AutoItem().AlignRight().Column(column =>
                {
                    column.Item().AlignRight().Text($"VERSION {request.Version.VersionNumber}")
                        .FontSize(RosterDocumentStyle.Heading(settings))
                        .Bold();

                    column.Item().AlignRight().Text(Status(request.Version))
                        .FontSize(RosterDocumentStyle.Small(settings))
                        .FontColor(RosterDocumentStyle.Muted);
                });
            }));

        page.Footer().Element(footer => footer
            .PaddingTop(settings.BaseFontPoints * 0.3f)
            .Row(row =>
            {
                row.RelativeItem().Text(explainsTheLeaderMark
                        ? $"{RosterDocumentStyle.LeaderMark} marks the line leader."
                        : string.Empty)
                    .FontSize(RosterDocumentStyle.Small(settings))
                    .FontColor(RosterDocumentStyle.Muted);

                row.AutoItem().Text(text =>
                {
                    text.DefaultTextStyle(style => style
                        .FontSize(RosterDocumentStyle.Small(settings))
                        .FontColor(RosterDocumentStyle.Muted));

                    text.CurrentPageNumber();
                    text.Span(" of ");
                    text.TotalPages();
                });
            }));
    }

    private static void FullSheetBody(
        IContainer container,
        PrintRequest request,
        NameBook names,
        RosterDay day,
        Attendance present)
    {
        var settings = request.Settings;

        container.PaddingVertical(settings.BaseFontPoints * 0.5f).Column(column =>
        {
            foreach (var line in request.Lines.OrderBy(line => line.DisplayOrder))
            {
                // Somebody marked absent is left out entirely rather than struck through.
                // The wall sheet answers "who is on this line", and a name on it that is
                // not going to be there answers it wrongly. What changed since the last
                // print belongs on the amendment slip.
                var onLine = day.Assignments
                    .Where(assignment => assignment.LineId == line.Id)
                    .Where(assignment => !present.IsAbsent(assignment))
                    .OrderByDescending(assignment => assignment.Role == AssignmentRole.LineLeader)
                    .ThenBy(assignment => names.Of(assignment.EmployeeId), StringComparer.CurrentCulture)
                    .ToList();

                column.Item()
                    .PaddingBottom(settings.BaseFontPoints * 0.5f)
                    .BorderBottom(1)
                    .BorderColor(RosterDocumentStyle.Rule)
                    .PaddingBottom(settings.BaseFontPoints * 0.5f)
                    .Row(row =>
                    {
                        row.ConstantItem(settings.BaseFontPoints * 12).Column(heading =>
                        {
                            heading.Item().Text(line.Name)
                                .FontSize(RosterDocumentStyle.Heading(settings))
                                .Bold();

                            heading.Item().Text($"{onLine.Count} of {line.RequiredHeadcount}")
                                .FontSize(RosterDocumentStyle.Small(settings))
                                .FontColor(RosterDocumentStyle.Muted);
                        });

                        row.RelativeItem().Element(people => People(people, settings, onLine, names));
                    });
            }
        });
    }

    private static void PerLineBody(
        IContainer container,
        PrintRequest request,
        NameBook names,
        ProductionLine line,
        Attendance present)
    {
        var settings = request.Settings;

        container.PaddingVertical(settings.BaseFontPoints * 0.5f).Column(column =>
        {
            foreach (var day in request.Roster.Days)
            {
                var onLine = day.Assignments
                    .Where(assignment => assignment.LineId == line.Id)
                    .Where(assignment => !present.IsAbsent(assignment))
                    .OrderByDescending(assignment => assignment.Role == AssignmentRole.LineLeader)
                    .ThenBy(assignment => names.Of(assignment.EmployeeId), StringComparer.CurrentCulture)
                    .ToList();

                column.Item()
                    .PaddingBottom(settings.BaseFontPoints * 0.6f)
                    .BorderBottom(1)
                    .BorderColor(RosterDocumentStyle.Rule)
                    .PaddingBottom(settings.BaseFontPoints * 0.6f)
                    .Row(row =>
                    {
                        row.ConstantItem(settings.BaseFontPoints * 11).Text(names.DayTitle(day))
                            .FontSize(RosterDocumentStyle.Heading(settings))
                            .Bold();

                        row.RelativeItem().Element(people => People(people, settings, onLine, names));
                    });
            }
        });
    }

    private static void AmendmentBody(
        IContainer container,
        PrintRequest request,
        NameBook names,
        IReadOnlyList<RosterChange> changes)
    {
        var settings = request.Settings;

        container.PaddingVertical(settings.BaseFontPoints * 0.5f).Column(column =>
        {
            column.Item().PaddingBottom(settings.BaseFontPoints).Text(
                "Changes since the sheet on the wall. Everything not listed here is unchanged.")
                .FontSize(RosterDocumentStyle.Body(settings));

            if (changes.Count == 0)
            {
                column.Item().Text("Nothing has changed.")
                    .FontSize(RosterDocumentStyle.Heading(settings))
                    .Bold();

                return;
            }

            foreach (var group in changes.GroupBy(change => change.Date).OrderBy(group => group.Key))
            {
                column.Item().PaddingTop(settings.BaseFontPoints * 0.5f).Text(DayName(group.Key))
                    .FontSize(RosterDocumentStyle.Heading(settings))
                    .Bold();

                foreach (var change in group)
                {
                    column.Item()
                        .BorderBottom(1)
                        .BorderColor(RosterDocumentStyle.Rule)
                        .PaddingVertical(settings.BaseFontPoints * 0.3f)
                        .Row(row =>
                        {
                            // The verb comes first and in capitals, so somebody scanning the
                            // slip sees what happened before they see who it happened to.
                            row.ConstantItem(settings.BaseFontPoints * 7).Text(Verb(change.Kind))
                                .FontSize(RosterDocumentStyle.Body(settings))
                                .Bold();

                            row.RelativeItem().Text(names.Of(change.EmployeeId))
                                .FontSize(RosterDocumentStyle.Body(settings));

                            row.RelativeItem().Text(Movement(change, names))
                                .FontSize(RosterDocumentStyle.Body(settings))
                                .FontColor(RosterDocumentStyle.Muted);
                        });
                }
            }
        });
    }

    /// <summary>
    /// The people on a line. A leader is marked three ways at once: a filled block, bold
    /// type, and the word itself. Colour is never the only thing carrying the meaning,
    /// because the sheet has to survive a monochrome printer and a reader who cannot
    /// distinguish the accents.
    /// </summary>
    private static void People(
        IContainer container,
        PrintSettings settings,
        IReadOnlyList<Assignment> assignments,
        NameBook names)
    {
        if (assignments.Count == 0)
        {
            container.Text("NOBODY ASSIGNED")
                .FontSize(RosterDocumentStyle.Body(settings))
                .Bold();

            return;
        }

        container.Column(column =>
        {
            foreach (var assignment in assignments)
            {
                var isLeader = assignment.Role == AssignmentRole.LineLeader;

                column.Item()
                    .Background(isLeader ? RosterDocumentStyle.LeaderBackground : Colors.White)
                    .PaddingVertical(settings.BaseFontPoints * 0.15f)
                    .PaddingHorizontal(settings.BaseFontPoints * 0.3f)
                    .Row(row =>
                    {
                        row.ConstantItem(settings.BaseFontPoints * 1.4f).Text(isLeader
                            ? RosterDocumentStyle.LeaderMark
                            : string.Empty);

                        var name = row.RelativeItem().Text(names.Of(assignment.EmployeeId))
                            .FontSize(RosterDocumentStyle.Body(settings));

                        if (isLeader)
                        {
                            name.Bold();
                        }

                        if (isLeader)
                        {
                            row.AutoItem().Text(RosterDocumentStyle.LeaderLabel)
                                .FontSize(RosterDocumentStyle.Small(settings))
                                .Bold();
                        }
                    });
            }
        });
    }

    private static string Status(RosterVersion version) =>
        version.Status == RosterStatus.Published ? "PUBLISHED" : "DRAFT, NOT FOR THE WALL";

    private static string Verb(RosterChangeKind kind) => kind switch
    {
        RosterChangeKind.Added => "ADDED",
        RosterChangeKind.Removed => "OFF",
        RosterChangeKind.Moved => "MOVED",
        RosterChangeKind.RoleChanged => "LEADER",
        _ => string.Empty,
    };

    private static string Movement(RosterChange change, NameBook names) => change.Kind switch
    {
        RosterChangeKind.Added => $"to {names.Line(change.ToLineId)}",
        RosterChangeKind.Removed => $"from {names.Line(change.FromLineId)}",
        RosterChangeKind.Moved => $"{names.Line(change.FromLineId)} to {names.Line(change.ToLineId)}",
        RosterChangeKind.RoleChanged => change.NowLeading
            ? $"now leading {names.Line(change.ToLineId)}"
            : $"no longer leading {names.Line(change.ToLineId)}",
        _ => string.Empty,
    };

    private static string DayName(DateOnly date) =>
        date.ToString("dddd d MMMM", CultureInfo.CurrentCulture);

    /// <summary>
    /// Resolves identifiers to names at the point of printing, and nowhere earlier. Warnings
    /// and assignments carry identifiers precisely so that personal data does not travel
    /// further than the page it is printed on.
    /// </summary>
    private sealed class NameBook
    {
        private readonly Dictionary<Guid, string> _employees;
        private readonly Dictionary<Guid, string> _lines;
        private readonly Dictionary<Guid, ShiftName> _shifts;

        public NameBook(PrintRequest request)
        {
            _employees = request.Employees
                .GroupBy(employee => employee.Id)
                .ToDictionary(group => group.Key, group => group.First().FullName);

            _lines = request.Lines
                .GroupBy(line => line.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);

            _shifts = request.Shifts
                .GroupBy(shift => shift.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }

        public string Of(Guid employeeId) => _employees.GetValueOrDefault(employeeId, "Unknown");

        public string Line(Guid? lineId) =>
            lineId is { } id ? _lines.GetValueOrDefault(id, "Unknown") : "off";

        public string DayTitle(RosterDay day) => DayName(day.Date);

        public string ShiftTitle(RosterDay day) =>
            _shifts.TryGetValue(day.ShiftId, out var shift) && shift != ShiftName.Day
                ? $"{DayName(day.Date)} — {shift}"
                : DayName(day.Date);
    }
}
