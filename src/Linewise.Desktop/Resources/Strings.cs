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

    public static string ChipEdited => Get(nameof(ChipEdited));

    public static string UnknownEmployee => Get(nameof(UnknownEmployee));

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
