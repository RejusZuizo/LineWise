using Linewise.Application.Persistence;
using Linewise.Application.Rostering;
using Linewise.Desktop.ViewModels;
using Linewise.Domain.Entities;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// Adding and editing lines, and the collection behaviour that decides whether the window
/// is usable.
/// </summary>
public sealed class LineEditorViewModelTests
{
    /// <summary>
    /// The regression this file exists for. Adding used to reload, and reloading clears the
    /// collection; a cleared ObservableCollection raises a reset, the list rebuilds every
    /// row container, and whichever text box was being edited is destroyed along with the
    /// caret in it. Keeping the existing instances is what keeps the editors alive.
    /// </summary>
    [Fact]
    public async Task Adding_a_line_leaves_the_rows_already_on_screen_untouched()
    {
        var repository = new FakeConfiguration(Line("Ovens"), Line("Packing"));
        var editor = new LineEditorViewModel(repository);

        await editor.LoadAsync();

        var before = editor.Lines.ToList();

        editor.NewLineName = "Chilled prep";
        await editor.AddCommand.ExecuteAsync(null);

        Assert.Equal(3, editor.Lines.Count);

        // Same objects, not equal ones. A rebuilt row is a new control and a lost caret.
        Assert.Same(before[0], editor.Lines[0]);
        Assert.Same(before[1], editor.Lines[1]);
    }

    [Fact]
    public async Task Adding_a_line_saves_it_and_clears_the_box_for_the_next_one()
    {
        var repository = new FakeConfiguration();
        var editor = new LineEditorViewModel(repository);

        await editor.LoadAsync();

        editor.NewLineName = "Ovens";
        editor.NewLineHeadcount = 6;
        await editor.AddCommand.ExecuteAsync(null);

        var saved = Assert.Single(repository.Saved);
        Assert.Equal("Ovens", saved.Name);
        Assert.Equal(6, saved.RequiredHeadcount);
        Assert.Equal(string.Empty, editor.NewLineName);
    }

    /// <summary>
    /// A name of spaces is not a line. Pressing add with an empty box should do nothing at
    /// all rather than store something nobody can identify on a wall sheet.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task An_empty_name_adds_nothing(string name)
    {
        var repository = new FakeConfiguration();
        var editor = new LineEditorViewModel(repository);

        editor.NewLineName = name;
        await editor.AddCommand.ExecuteAsync(null);

        Assert.Empty(repository.Saved);
        Assert.Empty(editor.Lines);
    }

    /// <summary>
    /// Lines are walked in an order, and a new one goes on the end of it rather than
    /// silently taking a number somebody else already has.
    /// </summary>
    [Fact]
    public async Task A_new_line_goes_on_the_end()
    {
        var repository = new FakeConfiguration(Line("Ovens", order: 1), Line("Packing", order: 2));
        var editor = new LineEditorViewModel(repository);

        await editor.LoadAsync();

        editor.NewLineName = "Wash";
        await editor.AddCommand.ExecuteAsync(null);

        Assert.Equal(3, Assert.Single(repository.Saved).DisplayOrder);
    }

    /// <summary>
    /// Editing a name must not drop what this screen does not show. Skills are configured
    /// elsewhere and would otherwise be quietly erased by a rename.
    /// </summary>
    [Fact]
    public void Editing_a_line_carries_through_the_fields_this_screen_does_not_show()
    {
        var skill = Guid.NewGuid();

        var row = new EditableLineViewModel(new ProductionLine
        {
            Id = Guid.NewGuid(),
            Name = "Ovens",
            RequiredHeadcount = 4,
            RequiredSkillIds = new HashSet<Guid> { skill },
        });

        row.Name = "  Ovens night  ";

        var line = row.ToLine();

        Assert.Equal("Ovens night", line.Name);
        Assert.Contains(skill, line.RequiredSkillIds);
    }

    private static ProductionLine Line(string name, int order = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            RequiredHeadcount = 4,
            DisplayOrder = order,
        };

    private sealed class FakeConfiguration : IConfigurationRepository
    {
        private readonly List<ProductionLine> _existing;

        public FakeConfiguration(params ProductionLine[] existing) => _existing = [.. existing];

        public List<ProductionLine> Saved { get; } = [];

        public Task<RosterConfiguration> GetAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new RosterConfiguration { Lines = _existing });

        public Task SaveLineAsync(ProductionLine line, CancellationToken cancellationToken = default)
        {
            Saved.Add(line);
            return Task.CompletedTask;
        }

        public Task SaveEmployeeAsync(Employee employee, CancellationToken cancellationToken = default) =>
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

    /// <summary>
    /// Editing a line must not silently drop what another screen configured on it. The
    /// description and the layout notes join the skills in that category.
    /// </summary>
    [Fact]
    public void Editing_a_line_keeps_its_description_and_layout_notes()
    {
        var line = new ProductionLine
        {
            Id = Guid.NewGuid(),
            Name = "Ovens",
            RequiredHeadcount = 4,
            Description = "Par-baked goods.",
            LayoutNotes = "Loader at the cold end, two on the belt.",
        };

        var editable = new EditableLineViewModel(line);

        Assert.Equal("Par-baked goods.", editable.Description);
        Assert.True(editable.HasNotes);

        editable.RequiredHeadcount = 6;

        var saved = editable.ToLine();

        Assert.Equal(6, saved.RequiredHeadcount);
        Assert.Equal("Par-baked goods.", saved.Description);
        Assert.Equal("Loader at the cold end, two on the belt.", saved.LayoutNotes);
    }

    [Fact]
    public void Notes_are_trimmed_and_an_empty_line_has_none()
    {
        var editable = new EditableLineViewModel(new ProductionLine
        {
            Id = Guid.NewGuid(),
            Name = "Packing",
            RequiredHeadcount = 2,
        });

        Assert.False(editable.HasNotes);

        editable.LayoutNotes = "  Two at the taper.  ";

        Assert.Equal("Two at the taper.", editable.ToLine().LayoutNotes);
    }
}
