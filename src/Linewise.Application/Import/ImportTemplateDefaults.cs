using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Application.Import;

/// <summary>
/// The template a first run starts from.
/// </summary>
/// <remarks>
/// A template describes the shape of one factory's sheet, and until phase 7 there is no
/// screen to build one on. Without a default there is nothing to parse against and the
/// import button has nothing to do, which would leave the product unusable until a
/// configuration screen that is two phases away.
/// <para>
/// This describes the layout in front of us: names down the first column, dates across the
/// first row, and the words the site writes in the cells. It is a starting point, not a
/// claim about every factory — the whole reason the entity exists is that layouts differ,
/// and phase 7 lets somebody change it without a rebuild.
/// </para>
/// </remarks>
public static class ImportTemplateDefaults
{
    public const string DefaultName = "Written marks, names in column A";

    public static ImportTemplate Create() => new()
    {
        Id = Guid.NewGuid(),
        Name = DefaultName,
        HeaderRowIndex = 1,
        NameColumnIndex = 1,
        FirstDateColumnIndex = 2,

        // Text rules only. Colour is what the site used to do and may do again, and the
        // reader tries text first regardless of order, so adding colour rules here would
        // only guess at shades nobody has shown us.
        StatusRules =
        [
            new CellStatusRule(CellMatchKind.Text, "work", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.Text, "working", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.Text, "w", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.Text, "yes", AvailabilityStatus.Working),
            new CellStatusRule(CellMatchKind.Text, "ot", AvailabilityStatus.Overtime),
            new CellStatusRule(CellMatchKind.Text, "overtime", AvailabilityStatus.Overtime),
            new CellStatusRule(CellMatchKind.Text, "holiday", AvailabilityStatus.Holiday),
            new CellStatusRule(CellMatchKind.Text, "hol", AvailabilityStatus.Holiday),
            new CellStatusRule(CellMatchKind.Text, "a/l", AvailabilityStatus.Holiday),
            new CellStatusRule(CellMatchKind.Text, "off", AvailabilityStatus.Off),
        ],

        // Silence is read as absence, never as availability. Assuming somebody is in
        // because nobody wrote anything is how a line is short on the morning.
        EmptyCellStatus = AvailabilityStatus.Off,

        // A mark nobody mentioned gets reported rather than quietly becoming a day off.
        ReportUnrecognisedCells = true,
    };
}
