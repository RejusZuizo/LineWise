using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Application.Validation;
using Linewise.Desktop.ViewModels;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// Entering the rules the engine has honoured since phase 1, and being told when they
/// cannot all be true at once.
/// </summary>
public sealed class PeopleViewModelTests
{
    private static readonly Guid Ovens = Guid.NewGuid();
    private static readonly Guid Packing = Guid.NewGuid();

    /// <summary>
    /// The engine reads an unset preference as freedom to place somebody anywhere. If the
    /// screen cannot express that, every person acquires an opinion about every line the
    /// first time somebody opens it.
    /// </summary>
    [Fact]
    public async Task A_line_nobody_has_an_opinion_about_stores_no_preference()
    {
        var editor = await Loaded();

        Assert.Equal(2, editor.Selected!.Lines.Count);
        Assert.All(editor.Selected.Lines, line => Assert.Null(line.Type));
        Assert.Empty(editor.Selected.ToPreferences());
    }

    [Fact]
    public async Task A_ranked_wish_is_stored_with_its_rank()
    {
        var editor = await Loaded();

        var ovens = editor.Selected!.Lines.First(line => line.LineId == Ovens);
        ovens.Type = PreferenceType.Preferred;
        ovens.Rank = 2;

        var preference = Assert.Single(editor.Selected.ToPreferences());

        Assert.Equal(Ovens, preference.LineId);
        Assert.Equal(PreferenceType.Preferred, preference.Type);
        Assert.Equal(2, preference.Rank);
    }

    /// <summary>
    /// Barred from a line and allowed to run it cannot both be true. The validator would
    /// report it; the screen should not let it be entered in the first place.
    /// </summary>
    [Fact]
    public async Task Blocking_a_line_withdraws_permission_to_lead_or_assist_on_it()
    {
        var editor = await Loaded();

        var ovens = editor.Selected!.Lines.First(line => line.LineId == Ovens);
        ovens.CanLead = true;
        ovens.CanAssist = true;

        ovens.Type = PreferenceType.Blocked;

        Assert.False(ovens.CanLead);
        Assert.False(ovens.CanAssist);

        // Ovens specifically, not everything. Blocking one line says nothing about the
        // others, and an assertion that everything vanished would pass for the wrong reason.
        Assert.DoesNotContain(editor.Selected.ToLeaderEligibilities(), e => e.LineId == Ovens);
        Assert.DoesNotContain(editor.Selected.ToAssistantEligibilities(), e => e.LineId == Ovens);
        Assert.Contains(editor.Selected.ToLeaderEligibilities(), e => e.LineId == Packing);
    }

    [Fact]
    public async Task Rank_is_only_meaningful_for_a_wish_or_a_requirement()
    {
        var editor = await Loaded();

        var ovens = editor.Selected!.Lines.First(line => line.LineId == Ovens);

        ovens.Type = PreferenceType.Preferred;
        Assert.True(ovens.IsRankRelevant);

        ovens.Type = PreferenceType.Mandatory;
        Assert.True(ovens.IsRankRelevant);

        ovens.Type = PreferenceType.Blocked;
        Assert.False(ovens.IsRankRelevant);

        ovens.Type = null;
        Assert.False(ovens.IsRankRelevant);
    }

    /// <summary>
    /// The validator has existed since the engine did and nothing called it from the
    /// application until now. A line nobody may lead is the case it was written for.
    /// </summary>
    [Fact]
    public async Task Checking_reports_a_line_nobody_can_lead()
    {
        var configuration = new FakeConfiguration(
            [Line(Ovens, "Ovens")],
            leadersEligible: false);

        var editor = new PeopleViewModel(configuration, new RosterRuleValidator());
        await editor.LoadAsync();

        await editor.CheckRulesCommand.ExecuteAsync(null);

        Assert.True(editor.HasBeenChecked);
        Assert.NotEmpty(editor.Impossibilities);
        Assert.False(editor.IsPossible);
    }

    /// <summary>
    /// The case that shipped broken. A line asking for operating assistants nobody may be
    /// threw a FormatException out of the validator, because the warning code collided with
    /// another and fetched a message expecting more arguments than it was given.
    /// </summary>
    [Fact]
    public async Task Checking_reports_a_line_nobody_can_assist_on()
    {
        var configuration = new FakeConfiguration(
        [
            new ProductionLine
            {
                Id = Ovens,
                Name = "Ovens",
                RequiredHeadcount = 3,
                RequiredOperatingAssistants = 1,
            },
        ]);

        var editor = new PeopleViewModel(configuration, new RosterRuleValidator());
        await editor.LoadAsync();

        await editor.CheckRulesCommand.ExecuteAsync(null);

        Assert.Contains(
            editor.Impossibilities,
            issue => issue.Contains("operating assistants", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task A_workable_configuration_says_so()
    {
        var editor = await Loaded();

        await editor.CheckRulesCommand.ExecuteAsync(null);

        Assert.True(editor.HasBeenChecked);
        Assert.Empty(editor.Impossibilities);
        Assert.True(editor.IsPossible);
    }

    [Fact]
    public async Task Searching_narrows_the_list_without_losing_anybody()
    {
        var editor = await Loaded();

        editor.Search = "Bram";

        Assert.Single(editor.Visible);
        Assert.Equal(2, editor.People.Count);

        editor.Search = string.Empty;

        Assert.Equal(2, editor.Visible.Count);
    }

    private static async Task<PeopleViewModel> Loaded(params ProductionLine[] lines)
    {
        var configuration = new FakeConfiguration(lines.Length == 0
            ? [Line(Ovens, "Ovens"), Line(Packing, "Packing")]
            : lines);

        var editor = new PeopleViewModel(configuration, new RosterRuleValidator());

        await editor.LoadAsync();

        return editor;
    }

    private static ProductionLine Line(Guid id, string name) =>
        new() { Id = id, Name = name, RequiredHeadcount = 2 };

    private sealed class FakeConfiguration : IConfigurationRepository
    {
        private readonly RosterConfiguration _configuration;

        public FakeConfiguration(IReadOnlyList<ProductionLine> lines, bool leadersEligible = true)
        {
            Employee[] employees =
            [
                new() { Id = Guid.NewGuid(), FullName = "Ada Fictional" },
                new() { Id = Guid.NewGuid(), FullName = "Bram Invented" },
            ];

            // Somebody has to be able to lead each line, or the configuration is genuinely
            // impossible and the validator is right to say so.
            var eligibilities = leadersEligible
                ? employees
                    .SelectMany(employee => lines.Select(line => new LeaderEligibility
                    {
                        EmployeeId = employee.Id,
                        LineId = line.Id,
                    }))
                    .ToList()
                : [];

            _configuration = new RosterConfiguration
            {
                Lines = lines,
                Employees = employees,
                LeaderEligibilities = eligibilities,
            };
        }

        public Task<RosterConfiguration> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_configuration);

        public Task SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveLineAsync(ProductionLine line, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SaveSkillAsync(Skill skill, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplacePreferencesAsync(
            Guid employeeId,
            IReadOnlyList<LinePreference> preferences,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceLeaderEligibilityAsync(
            Guid employeeId,
            IReadOnlyList<LeaderEligibility> eligibilities,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ReplaceOperatingAssistantEligibilityAsync(
            Guid employeeId,
            IReadOnlyList<OperatingAssistantEligibility> eligibilities,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PrintSettings> GetPrintSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PrintSettings { Id = Guid.Empty });

        public Task SavePrintSettingsAsync(
            PrintSettings settings,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
