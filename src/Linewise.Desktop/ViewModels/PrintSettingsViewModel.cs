using CommunityToolkit.Mvvm.ComponentModel;
using Linewise.Domain.Entities;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// How the printed sheet should look.
/// </summary>
/// <remarks>
/// Every one of these has been stored, read and honoured by the printer since phase 4, and
/// none of them could be changed without editing the database by hand. This is the screen
/// they were always waiting for.
/// <para>
/// Saved as each is changed rather than behind a save button. There is nothing here to
/// review before committing, and a settings panel that needs saving is one somebody closes
/// without saving.
/// </para>
/// </remarks>
public sealed partial class PrintSettingsViewModel : ObservableObject
{
    private readonly Func<PrintSettings, Task> _save;
    private readonly PrintSettings _original;
    private bool _loading;

    [ObservableProperty]
    private string _companyName;

    [ObservableProperty]
    private PaperSize _paperSize;

    [ObservableProperty]
    private PageOrientation _orientation;

    /// <summary>
    /// The one number everything else on the sheet scales from, because the distance a
    /// roster is read from is a property of the wall it is pinned to. ADR 0010.
    /// </summary>
    [ObservableProperty]
    private int _baseFontPoints;

    /// <summary>
    /// Off by default. Nothing on the printed sheet may be distinguished by colour alone,
    /// so line accents are decoration for a site that has checked its printer, never
    /// information. ADR 0010.
    /// </summary>
    [ObservableProperty]
    private bool _useAccentColours;

    public PrintSettingsViewModel(PrintSettings settings, Func<PrintSettings, Task> save)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(save);

        _original = settings;
        _save = save;

        _loading = true;
        _companyName = settings.CompanyName;
        _paperSize = settings.PaperSize;
        _orientation = settings.Orientation;
        _baseFontPoints = settings.BaseFontPoints;
        _useAccentColours = settings.UseAccentColours;
        _loading = false;
    }

    public static IReadOnlyList<PaperSize> PaperSizes { get; } = Enum.GetValues<PaperSize>();

    public static IReadOnlyList<PageOrientation> Orientations { get; } =
        Enum.GetValues<PageOrientation>();

    /// <summary>
    /// Carries the untouched fields through, the logo included. A settings panel that does
    /// not show something must not quietly drop it.
    /// </summary>
    public PrintSettings ToSettings() => _original with
    {
        CompanyName = CompanyName.Trim(),
        PaperSize = PaperSize,
        Orientation = Orientation,
        BaseFontPoints = Math.Clamp(BaseFontPoints, 8, 36),
        UseAccentColours = UseAccentColours,
    };

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Not while the constructor is filling the fields in, which would write the settings
        // back five times on the way to the screen.
        if (!_loading)
        {
            _ = _save(ToSettings());
        }
    }
}
