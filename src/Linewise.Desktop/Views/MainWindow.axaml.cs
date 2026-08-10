using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Linewise.Desktop.ViewModels;

namespace Linewise.Desktop.Views;

public sealed partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    /// <summary>
    /// The view model arrives through the constructor rather than being created here. No
    /// logic lives in code behind, and that rule is easier to keep from the first window
    /// than to reintroduce at the fourth.
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Parameterless for the Avalonia designer, which cannot use the container.</summary>
    public MainWindow()
        : this(new MainWindowViewModel())
    {
    }

    /// <summary>
    /// The load is started when the window opens rather than in the constructor, so that a
    /// slow read draws an empty window and then fills it, instead of delaying the window
    /// appearing at all. Cold start to a usable window is a stated target.
    /// </summary>
    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        await _viewModel.LoadAsync(DateOnly.FromDateTime(DateTime.Today)).ConfigureAwait(true);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
