using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Persistence;
using Linewise.Desktop.Resources;
using Linewise.Domain.Entities;
using Serilog;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// Adding and editing production lines.
/// </summary>
/// <remarks>
/// Pulled forward from phase 7, because nothing in this application draws anything until
/// lines exist and a first run has none. This is the minimum that makes the product usable
/// on day one: a name, how many people the line needs, the order it appears in, and a
/// colour. Skills, per-day demand and the rest stay in phase 7 where they belong.
/// </remarks>
public sealed partial class LineEditorViewModel : ObservableObject
{
    private readonly IConfigurationRepository? _configuration;

    [ObservableProperty]
    private string _newLineName = string.Empty;

    [ObservableProperty]
    private int _newLineHeadcount = 4;

    [ObservableProperty]
    private string _status = string.Empty;

    public LineEditorViewModel(IConfigurationRepository configuration) => _configuration = configuration;

    /// <summary>For the Avalonia designer, which cannot resolve from the container.</summary>
    public LineEditorViewModel()
    {
    }

    public ObservableCollection<EditableLineViewModel> Lines { get; } = [];

    public bool HasLines => Lines.Count > 0;

    /// <summary>
    /// A small fixed palette rather than a colour picker. Each line needs to be
    /// distinguishable from the others at a glance and nothing more; a free choice invites
    /// two lines a shade apart, which is exactly the distinction that fails on the wall.
    /// </summary>
    public static IReadOnlyList<string> Palette { get; } =
    [
        "#2F6F8F",
        "#7A4E8C",
        "#8C5A2B",
        "#3F7A52",
        "#8C3B4A",
        "#4A5A8C",
        "#6B7A2F",
        "#8C6B2F",
        "#2F7A7A",
        "#5B6670",
    ];

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_configuration is null)
        {
            return;
        }

        var configuration = await _configuration.GetAsync(cancellationToken).ConfigureAwait(true);

        Lines.Clear();

        foreach (var line in configuration.Lines.OrderBy(line => line.DisplayOrder))
        {
            Lines.Add(new EditableLineViewModel(line));
        }

        OnPropertyChanged(nameof(HasLines));
    }

    [RelayCommand]
    private async Task AddAsync(CancellationToken cancellationToken)
    {
        if (_configuration is null || string.IsNullOrWhiteSpace(NewLineName))
        {
            return;
        }

        var line = new ProductionLine
        {
            Id = Guid.NewGuid(),
            Name = NewLineName.Trim(),
            RequiredHeadcount = Math.Max(1, NewLineHeadcount),

            // Appended rather than inserted. The order lines appear in is the order the
            // factory walks them, and a new line goes on the end until somebody says
            // otherwise.
            DisplayOrder = Lines.Count == 0 ? 1 : Lines.Max(existing => existing.DisplayOrder) + 1,
            AccentColour = Palette[Lines.Count % Palette.Count],
        };

        await _configuration.SaveLineAsync(line, cancellationToken).ConfigureAwait(true);

        Log.Information("Added line {LineId}, needing {Headcount}.", line.Id, line.RequiredHeadcount);

        NewLineName = string.Empty;
        Status = Strings.LineAdded(line.Name);

        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_configuration is null)
        {
            return;
        }

        foreach (var line in Lines)
        {
            await _configuration.SaveLineAsync(line.ToLine(), cancellationToken).ConfigureAwait(true);
        }

        Status = Strings.LinesSaved(Lines.Count);
        Log.Information("Saved {Count} lines.", Lines.Count);
    }
}

/// <summary>One line, editable. Kept apart from the domain record, which is immutable.</summary>
public sealed partial class EditableLineViewModel : ObservableObject
{
    private readonly ProductionLine _original;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private int _requiredHeadcount;

    [ObservableProperty]
    private int _displayOrder;

    /// <summary>
    /// Out of the line's headcount rather than on top of it. Zero is the ordinary answer
    /// and keeps a line behaving exactly as it did before the role existed.
    /// </summary>
    [ObservableProperty]
    private int _requiredOperatingAssistants;

    public EditableLineViewModel(ProductionLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        _original = line;
        _name = line.Name;
        _requiredHeadcount = line.RequiredHeadcount;
        _displayOrder = line.DisplayOrder;
        _requiredOperatingAssistants = line.RequiredOperatingAssistants;
    }

    public string AccentColour => _original.AccentColour;

    /// <summary>
    /// Carries the untouched fields through. Editing a name must not silently drop the
    /// skills somebody configured on a screen this one does not show.
    /// </summary>
    public ProductionLine ToLine() => _original with
    {
        Name = Name.Trim(),
        RequiredHeadcount = Math.Max(1, RequiredHeadcount),
        DisplayOrder = DisplayOrder,
        RequiredOperatingAssistants = Math.Max(0, RequiredOperatingAssistants),
    };
}
