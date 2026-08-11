using System.Globalization;
using System.Resources;

namespace Linewise.Desktop.Resources;

/// <summary>
/// Every string the application shows a person, looked up from a resource file.
/// </summary>
/// <remarks>
/// Properties rather than a generated designer class, so the same accessor works from a
/// view model and from XAML through <c>{x:Static}</c>. A string written inline in a .axaml
/// file cannot be translated without a rebuild, which is the thing this exists to prevent.
/// <para>
/// A missing entry falls back to its key rather than throwing. A missing translation must
/// never take the roster down; an English key on screen is ugly and recoverable, and an
/// exception during a Monday morning sick call is neither.
/// </para>
/// </remarks>
public static class Strings
{
    private static readonly ResourceManager Manager =
        new("Linewise.Desktop.Resources.Strings", typeof(Strings).Assembly);

    public static string ProductName => Get(nameof(ProductName));

    public static string Starting => Get(nameof(Starting));

    public static string GenerateRoster => Get(nameof(GenerateRoster));

    public static string Print => Get(nameof(Print));

    public static string Working => Get(nameof(Working));

    public static string NothingToDrawYet => Get(nameof(NothingToDrawYet));

    public static string CouldNotLoad => Get(nameof(CouldNotLoad));

    public static string CouldNotGenerate => Get(nameof(CouldNotGenerate));

    public static string CouldNotPrint => Get(nameof(CouldNotPrint));

    public static string NothingToPrint => Get(nameof(NothingToPrint));

    public static string SentToPrinter => Get(nameof(SentToPrinter));

    public static string StatusDraft => Get(nameof(StatusDraft));

    public static string NoWarnings => Get(nameof(NoWarnings));

    public static string SeverityError => Get(nameof(SeverityError));

    public static string SeverityNotice => Get(nameof(SeverityNotice));

    public static string ContextSeparator => Get(nameof(ContextSeparator));

    public static string RoleLeader => Get(nameof(RoleLeader));

    public static string RoleOperatingAssistant => Get(nameof(RoleOperatingAssistant));

    public static string OperatingAssistantsNeeded => Get(nameof(OperatingAssistantsNeeded));

    public static string ChipEdited => Get(nameof(ChipEdited));

    public static string UnknownEmployee => Get(nameof(UnknownEmployee));

    public static string Lines => Get(nameof(Lines));

    public static string SetUpLines => Get(nameof(SetUpLines));

    public static string LineName => Get(nameof(LineName));

    public static string AddLine => Get(nameof(AddLine));

    public static string SaveLines => Get(nameof(SaveLines));

    public static string NoLinesYet => Get(nameof(NoLinesYet));

    public static string HeadcountNeeded => Get(nameof(HeadcountNeeded));

    public static string DisplayOrder => Get(nameof(DisplayOrder));

    public static string Close => Get(nameof(Close));

    public static string ToggleTheme => Get(nameof(ToggleTheme));

    public static string LinesColumnHeading => Get(nameof(LinesColumnHeading));

    public static string WarningsHeading => Get(nameof(WarningsHeading));

    public static string GettingStartedHeading => Get(nameof(GettingStartedHeading));

    public static string GettingStartedImport => Get(nameof(GettingStartedImport));

    public static string GettingStartedLines => Get(nameof(GettingStartedLines));

    public static string GettingStartedGenerate => Get(nameof(GettingStartedGenerate));

    public static string Import => Get(nameof(Import));

    public static string ImportTitle => Get(nameof(ImportTitle));

    public static string ChooseSheet => Get(nameof(ChooseSheet));

    public static string ImportReadyToReview => Get(nameof(ImportReadyToReview));

    public static string ImportNothingUsable => Get(nameof(ImportNothingUsable));

    public static string ImportCouldNotRead => Get(nameof(ImportCouldNotRead));

    public static string ImportCouldNotCommit => Get(nameof(ImportCouldNotCommit));

    public static string ImportNeedingAttention => Get(nameof(ImportNeedingAttention));

    public static string ImportAddAllAsTemporary => Get(nameof(ImportAddAllAsTemporary));

    public static string ImportAddAsTemporary => Get(nameof(ImportAddAsTemporary));

    public static string ImportCommit => Get(nameof(ImportCommit));

    public static string ImportSummary(int matched, int needingAttention, int days) =>
        Format(nameof(ImportSummary), matched, needingAttention, days);

    public static string ImportUnrecognised(int cells) => Format(nameof(ImportUnrecognised), cells);

    public static string ImportReason(string fileName) => Format(nameof(ImportReason), fileName);

    public static string ImportRow(int row) => Format(nameof(ImportRow), row);

    public static string ImportCommitted(int records, int added) =>
        added == 1 && records == 0
            ? Get("ImportCommittedOne")
            : Format("ImportCommittedMany", records, added);

    public static string LineAdded(string name) => Format(nameof(LineAdded), name);

    public static string LinesSaved(int count) =>
        count == 1 ? Get("LinesSavedOne") : Format("LinesSavedMany", count);

    public static string WindowTitle(string week, string state) =>
        Format(nameof(WindowTitle), ProductName, week, state);

    public static string WeekBeginning(DateOnly weekStart) =>
        Format(nameof(WeekBeginning), weekStart);

    public static string NoRosterStored(DateOnly weekStart) =>
        Format(nameof(NoRosterStored), weekStart);

    public static string StatusPublished(int version) =>
        Format(nameof(StatusPublished), version);

    public static string Counts(int placed, int unplaced, int overtime, int off) =>
        Format(nameof(Counts), placed, unplaced, overtime, off);

    public static string Headcount(int assigned, int required) =>
        Format(nameof(Headcount), assigned, required);

    public static string LineNeeds(int required) => Format(nameof(LineNeeds), required);

    /// <summary>
    /// Singular and plural are separate entries rather than an "s" appended in code. That
    /// trick is English, and it stops being correct in the first language anybody asks for.
    /// </summary>
    public static string LineShort(int days) =>
        days == 1 ? Get("LineShortOneDay") : Format("LineShortManyDays", days);

    public static string ErrorCount(int count) =>
        count == 1 ? Get("ErrorCountOne") : Format("ErrorCountMany", count);

    public static string NoticeCount(int count) =>
        count == 1 ? Get("NoticeCountOne") : Format("NoticeCountMany", count);

    public static string WarningsSummary(string errors, string notices) =>
        Format(nameof(WarningsSummary), errors, notices);

    private static string Get(string key) =>
        Manager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    private static string Format(string key, params object?[] arguments)
    {
        var template = Manager.GetString(key, CultureInfo.CurrentUICulture);

        return string.IsNullOrEmpty(template)
            ? key
            : string.Format(CultureInfo.CurrentCulture, template, arguments);
    }
}
