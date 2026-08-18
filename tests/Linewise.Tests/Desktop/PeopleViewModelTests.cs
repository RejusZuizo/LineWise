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
    /// A person with no lines in their list is the engine's freedom to place them anywhere.
    /// If the screen cannot express that, every person acquires an opinion about every line
    /// the first time somebody opens it.
    /// </summary>
    [Fact]
    public async Task A_person_with_no_lines_stores_no_preferences()
    {
        var editor = await Loaded(leadersEligible: false);

        Assert.Empty(editor.Selected!.Works);
        Assert.Empty(editor.Selected.Blocked);
        Assert.Empty(editor.Selected.ToPreferences());

        // Both lines are still on offer, which is what makes the empty list a starting
        // point rather than a dead end.
        Assert.Equal(2, editor.Selected.Addable.Count);
    }

    /// <summary>
    /// The change this screen exists for: priority is the order of a list, not a number
    /// somebody types into a box beside each line.
    /// </summary>
    [Fact]
    public async Task Priority_is_the_position_in_the_list()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        person.AddWorkedCommand.Execute(Line(Packing, "Packing"));

        var preferences = person.ToPreferences();

        Assert.Equal(Ovens, preferences.Single(p => p.Rank == 1).LineId);
        Assert.Equal(Packing, preferences.Single(p => p.Rank == 2).LineId);
        Assert.All(preferences, p => Assert.Equal(PreferenceType.Preferred, p.Type));
    }

    [Fact]
    public async Task Moving_a_line_up_makes_it_the_first_choice()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        person.AddWorkedCommand.Execute(Line(Packing, "Packing"));

        person.MoveUp(person.Works.Single(row => row.LineId == Packing));

        Assert.Equal(Packing, person.ToPreferences().Single(p => p.Rank == 1).LineId);
        Assert.Equal(1, person.Works[0].Position);
        Assert.True(person.Works[0].IsFirst);
        Assert.True(person.Works[1].IsLast);
    }

    /// <summary>
    /// A drag that lands where it started, and the arrows at the ends of the list. Neither
    /// should reorder anything, and neither should throw.
    /// </summary>
    [Fact]
    public async Task Moving_beyond_either_end_does_nothing()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        person.AddWorkedCommand.Execute(Line(Packing, "Packing"));

        person.MoveUp(person.Works[0]);
        person.MoveDown(person.Works[1]);

        Assert.Equal([Ovens, Packing], person.Works.Select(row => row.LineId));
    }

    /// <summary>
    /// Rank cannot be duplicated or skipped, because nothing stores it. That whole class of
    /// warning — two lines claiming the same choice — stops being possible to enter.
    /// </summary>
    [Fact]
    public async Task Ranks_are_always_one_two_three_with_no_gaps()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        person.AddWorkedCommand.Execute(Line(Packing, "Packing"));

        person.Remove(person.Works.Single(row => row.LineId == Ovens));

        var preference = Assert.Single(person.ToPreferences());
        Assert.Equal(1, preference.Rank);
    }

    [Fact]
    public async Task A_line_must_work_flag_is_stored_as_a_requirement()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        person.Works[0].IsMandatory = true;

        var preference = Assert.Single(person.ToPreferences());

        Assert.Equal(PreferenceType.Mandatory, preference.Type);

        // Still ranked. A requirement the engine may break on overtime still has a position
        // relative to the rest of the list.
        Assert.Equal(1, preference.Rank);
    }

    [Fact]
    public async Task A_refused_line_is_stored_as_blocked_and_carries_no_rank()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddBlockedCommand.Execute(Line(Ovens, "Ovens"));

        var preference = Assert.Single(person.ToPreferences());

        Assert.Equal(PreferenceType.Blocked, preference.Type);
        Assert.Equal(0, preference.Rank);
    }

    /// <summary>
    /// Worked and refused are two lists, so a line cannot be in both. The old screen made
    /// that unrepresentable with a radio group; this makes it unrepresentable by removing
    /// the line from what can be added.
    /// </summary>
    [Fact]
    public async Task A_line_cannot_be_both_worked_and_refused()
    {
        var editor = await Loaded(leadersEligible: false);
        var person = editor.Selected!;

        person.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));

        Assert.DoesNotContain(person.Addable, line => line.Id == Ovens);

        person.Remove(person.Works[0]);

        Assert.Contains(person.Addable, line => line.Id == Ovens);
    }

    /// <summary>
    /// Permission to lead used to be settable on a line nobody had an opinion about, and
    /// there is data on disk shaped that way. Dropping those rows would quietly take away
    /// permissions somebody had already granted.
    /// </summary>
    [Fact]
    public async Task An_existing_permission_to_lead_puts_that_line_in_the_worked_list()
    {
        var editor = await Loaded();

        var person = editor.Selected!;

        Assert.Equal(2, person.Works.Count);
        Assert.All(person.Works, row => Assert.True(row.CanLead));
        Assert.Contains(person.ToLeaderEligibilities(), e => e.LineId == Ovens);
    }

    [Fact]
    public async Task Removing_a_line_withdraws_the_permission_that_sat_on_it()
    {
        var editor = await Loaded();
        var person = editor.Selected!;

        person.Remove(person.Works.Single(row => row.LineId == Ovens));

        // Ovens specifically, not everything. Removing one line says nothing about the
        // others, and an assertion that everything vanished would pass for the wrong reason.
        Assert.DoesNotContain(person.ToLeaderEligibilities(), e => e.LineId == Ovens);
        Assert.Contains(person.ToLeaderEligibilities(), e => e.LineId == Packing);
    }

    /// <summary>
    /// Sixty names in one column is a wall. The groups are what the manager reads down when
    /// a line has lost its leader.
    /// </summary>
    [Fact]
    public async Task People_are_grouped_by_what_they_are_permitted_to_be()
    {
        var editor = await Loaded();

        var group = Assert.Single(editor.Groups);

        Assert.Equal(WorkerCapability.LineLeader, group.Capability);
        Assert.Equal(2, group.Count);
    }

    [Fact]
    public async Task An_empty_group_is_not_drawn()
    {
        var editor = await Loaded(leadersEligible: false);

        var group = Assert.Single(editor.Groups);

        Assert.Equal(WorkerCapability.LineWorker, group.Capability);
    }

    /// <summary>
    /// A heading is a legitimate thing to click. Losing the rules being edited because
    /// somebody collapsed a group would be its own bug.
    /// </summary>
    [Fact]
    public async Task Selecting_a_group_heading_keeps_the_person_on_screen()
    {
        var editor = await Loaded();
        var person = editor.Selected;

        editor.SelectedNode = editor.Groups[0];

        Assert.Same(person, editor.Selected);
        Assert.True(editor.HasSelection);
    }

    [Fact]
    public async Task Selecting_a_person_in_the_tree_selects_them()
    {
        var editor = await Loaded();
        var other = editor.Groups[0].People[1];

        editor.SelectedNode = other;

        Assert.Same(other, editor.Selected);
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

    /// <summary>
    /// A message saying a save happened is not an answer to "is what I am looking at
    /// saved". Somebody who edits, saves, edits again and walks away needs the second one.
    /// </summary>
    [Fact]
    public async Task Editing_says_the_screen_is_ahead_of_the_database()
    {
        var editor = await Loaded(leadersEligible: false);

        // Freshly loaded is neither. Nothing has been done yet.
        Assert.False(editor.HasUnsavedChanges);
        Assert.False(editor.IsSaved);

        editor.Selected!.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));

        Assert.True(editor.HasUnsavedChanges);
        Assert.False(editor.IsSaved);
    }

    [Fact]
    public async Task Saving_says_so_and_keeps_saying_so()
    {
        var editor = await Loaded(leadersEligible: false);

        editor.Selected!.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        await editor.SaveCommand.ExecuteAsync(null);

        Assert.True(editor.IsSaved);
        Assert.False(editor.HasUnsavedChanges);

        // And the next edit takes it back. A tick that stays on through the next change
        // would be worse than no tick at all.
        editor.Selected.Works[0].IsMandatory = true;

        Assert.True(editor.HasUnsavedChanges);
        Assert.False(editor.IsSaved);
    }

    /// <summary>
    /// A different person is a different question, and neither answer carries over.
    /// </summary>
    [Fact]
    public async Task Selecting_somebody_else_clears_the_indicator()
    {
        var editor = await Loaded(leadersEligible: false);

        editor.Selected!.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));
        Assert.True(editor.HasUnsavedChanges);

        editor.SelectedNode = editor.Groups[0].People[1];

        Assert.False(editor.HasUnsavedChanges);
        Assert.False(editor.IsSaved);
    }

    /// <summary>
    /// Selecting forty people in a row must not leave forty handlers behind, each
    /// announcing an edit to somebody nobody is looking at.
    /// </summary>
    [Fact]
    public async Task Editing_somebody_no_longer_selected_says_nothing()
    {
        var editor = await Loaded(leadersEligible: false);

        var first = editor.Selected!;
        editor.SelectedNode = editor.Groups[0].People[1];

        first.AddWorkedCommand.Execute(Line(Ovens, "Ovens"));

        Assert.False(editor.HasUnsavedChanges);
    }

    private static async Task<PeopleViewModel> Loaded(bool leadersEligible = true)
    {
        var configuration = new FakeConfiguration(
            [Line(Ovens, "Ovens"), Line(Packing, "Packing")],
            leadersEligible);

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
