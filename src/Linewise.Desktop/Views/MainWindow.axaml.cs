using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Linewise.Desktop.ViewModels;

namespace Linewise.Desktop.Views;

public sealed partial class MainWindow : Window
{
    /// <summary>
    /// The view model arrives through the constructor rather than being created here. No
    /// logic lives in code behind, and that rule is easier to keep from the first window
    /// than to reintroduce at the fourth.
    /// </summary>
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Parameterless for the Avalonia designer, which cannot use the container.</summary>
    public MainWindow()
        : this(new MainWindowViewModel())
    {
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
