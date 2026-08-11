using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Linewise.Desktop.ViewModels;

namespace Linewise.Desktop.Views;

public sealed partial class LineEditorWindow : Window
{
    private readonly LineEditorViewModel _viewModel;

    public LineEditorWindow(LineEditorViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Parameterless for the Avalonia designer, which cannot use the container.</summary>
    public LineEditorWindow()
        : this(new LineEditorViewModel())
    {
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        await _viewModel.LoadAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Closing a window is the one thing a view may do for itself. Routing it through a
    /// command would mean the view model holding a reference to its own window, which is
    /// the usual way MVVM starts leaking.
    /// </summary>
    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
