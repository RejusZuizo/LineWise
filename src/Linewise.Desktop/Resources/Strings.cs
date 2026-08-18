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

    public static string RoleLineWorker => Get(nameof(RoleLineWorker));

    public static string WorkforceHeading => Get(nameof(WorkforceHeading));

    public static string CapabilityBreakdown(int leaders, int assistants, int workers) =>
        Format(nameof(CapabilityBreakdown), leaders, assistants, workers);

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

    public static string NavRoster => Get(nameof(NavRoster));

    public static string NavDashboard => Get(nameof(NavDashboard));

    public static string NavRosterView => Get(nameof(NavRosterView));

    public static string ViewToday => Get(nameof(ViewToday));

    public static string ViewWholeWeek => Get(nameof(ViewWholeWeek));

    public static string DayHeading(DateOnly date) => Format(nameof(DayHeading), date);

    public static string DashboardHeading => Get(nameof(DashboardHeading));

    public static string DashboardSetupHeading => Get(nameof(DashboardSetupHeading));

    public static string DashboardNoAvailability => Get(nameof(DashboardNoAvailability));

    public static string DashboardNoRoster => Get(nameof(DashboardNoRoster));

    public static string DashboardSetupNote => Get(nameof(DashboardSetupNote));

    public static string NextStepLines => Get(nameof(NextStepLines));

    public static string NextStepPeople => Get(nameof(NextStepPeople));

    public static string NextStepImport => Get(nameof(NextStepImport));

    public static string NextStepGenerate => Get(nameof(NextStepGenerate));

    public static string NextStepReview => Get(nameof(NextStepReview));

    public static string OpenRoster => Get(nameof(OpenRoster));

    public static string DashboardLines(int lines) => Format(nameof(DashboardLines), lines);

    public static string DashboardPeople(int people, int agency) =>
        Format(nameof(DashboardPeople), people, agency);

    public static string DashboardAvailability(int people, int days) =>
        Format(nameof(DashboardAvailability), people, days);

    public static string DashboardRoster(int placed, int errors) =>
        Format(nameof(DashboardRoster), placed, errors);

    public static string People => Get(nameof(People));

    public static string PeopleSearch => Get(nameof(PeopleSearch));

    public static string PeopleNone => Get(nameof(PeopleNone));

    public static string PeoplePreferred => Get(nameof(PeoplePreferred));

    public static string PeopleMandatory => Get(nameof(PeopleMandatory));

    public static string PeopleBlocked => Get(nameof(PeopleBlocked));

    public static string PeopleRank => Get(nameof(PeopleRank));

    public static string PeopleCanLead => Get(nameof(PeopleCanLead));

    public static string PeopleCanAssist => Get(nameof(PeopleCanAssist));

    public static string PeopleTemporary => Get(nameof(PeopleTemporary));

    public static string PeopleNobody => Get(nameof(PeopleNobody));

    public static string GroupLineLeaders => Get(nameof(GroupLineLeaders));

    public static string GroupOperatingAssistants => Get(nameof(GroupOperatingAssistants));

    public static string GroupLineWorkers => Get(nameof(GroupLineWorkers));

    public static string GroupCount(int people) =>
        people == 1 ? Get("GroupCountOne") : Format("GroupCountMany", people);

    /// <summary>
    /// The first choice is the one that decides most rosters, so it is named rather than
    /// numbered. Everything below it is a number, which is what it is.
    /// </summary>
    public static string PeopleChoice(int position) =>
        position == 1 ? Get("PeopleChoiceFirst") : Format("PeopleChoiceOther", position);

    public static string PeopleWorksHeading => Get(nameof(PeopleWorksHeading));

    public static string PeopleWorksNone => Get(nameof(PeopleWorksNone));

    public static string PeopleBlockedHeading => Get(nameof(PeopleBlockedHeading));

    public static string PeopleBlockedNone => Get(nameof(PeopleBlockedNone));

    public static string PeopleAddWorked => Get(nameof(PeopleAddWorked));

    public static string PeopleAddBlocked => Get(nameof(PeopleAddBlocked));

    public static string PeopleMoveUp => Get(nameof(PeopleMoveUp));

    public static string PeopleMoveDown => Get(nameof(PeopleMoveDown));

    public static string PeopleRemoveLine => Get(nameof(PeopleRemoveLine));

    public static string PeopleMustWork => Get(nameof(PeopleMustWork));

    public static string PeopleDragHint => Get(nameof(PeopleDragHint));

    public static string PeopleDragHandle => Get(nameof(PeopleDragHandle));

    public static string SaveRules => Get(nameof(SaveRules));

    public static string CheckRules => Get(nameof(CheckRules));

    public static string RulesPossible => Get(nameof(RulesPossible));

    public static string RulesImpossibleHeading => Get(nameof(RulesImpossibleHeading));

    public static string PeopleSaved(string name) => Format(nameof(PeopleSaved), name);

    public static string PeopleSummary(int preferred, int mandatory, int blocked) =>
        Format(nameof(PeopleSummary), preferred, mandatory, blocked);

    public static string ShowWarnings => Get(nameof(ShowWarnings));

    public static string HideNavigation => Get(nameof(HideNavigation));

    public static string ShowNavigation => Get(nameof(ShowNavigation));

    public static string WarningGrouped(int count, string first) =>
        Format(nameof(WarningGrouped), count, first);

    public static string WarningCount(int count) => Format(nameof(WarningCount), count);

    public static string NavSetUp => Get(nameof(NavSetUp));

    public static string NavActions => Get(nameof(NavActions));

    public static string NavLines => Get(nameof(NavLines));

    public static string AddLineShort => Get(nameof(AddLineShort));

    public static string ManageLines => Get(nameof(ManageLines));

    public static string NoLinesSidebar => Get(nameof(NoLinesSidebar));

    public static string NavAppearance => Get(nameof(NavAppearance));

    public static string LineSummary(int headcount, int assistants) =>
        Format(nameof(LineSummary), headcount, assistants);

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

    public static string RoleAbsent => Get(nameof(RoleAbsent));

    public static string MarkAbsent => Get(nameof(MarkAbsent));

    public static string MarkBackIn => Get(nameof(MarkBackIn));

    public static string AbsenceNotInToday => Get(nameof(AbsenceNotInToday));

    public static string AbsenceHoliday => Get(nameof(AbsenceHoliday));

    public static string AbsenceSentHome => Get(nameof(AbsenceSentHome));

    public static string MarkedBackIn => Get(nameof(MarkedBackIn));

    public static string CellClosed => Get(nameof(CellClosed));

    public static string CloseLine => Get(nameof(CloseLine));

    public static string ReopenLine => Get(nameof(ReopenLine));

    public static string CouldNotCloseLine => Get(nameof(CouldNotCloseLine));

    public static string LineClosedFor(DateOnly date) => Format(nameof(LineClosedFor), date);

    public static string LineReopenedFor(DateOnly date) => Format(nameof(LineReopenedFor), date);

    public static string CouldNotMarkAbsent => Get(nameof(CouldNotMarkAbsent));

    /// <summary>
    /// Three entries rather than one with a plural rule in code, and the zero case says
    /// something different rather than reading "0 places to fill".
    /// </summary>
    public static string MarkedAbsent(int places) => places switch
    {
        0 => Get("MarkedAbsentNoPlaces"),
        1 => Get("MarkedAbsentOnePlace"),
        _ => Format("MarkedAbsentManyPlaces", places),
    };

    public static string CellAbsent(int count) =>
        count == 1 ? Get("CellAbsentOne") : Format("CellAbsentMany", count);

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
