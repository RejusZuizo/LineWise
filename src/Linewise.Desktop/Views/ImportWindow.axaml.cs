using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Linewise.Desktop.ViewModels;

namespace Linewise.Desktop.Views;

public sealed partial class ImportWindow : Window
{
    public ImportWindow(ImportViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    /// <summary>Parameterless for the Avalonia designer, which cannot use the container.</summary>
    public ImportWindow()
        : this(new ImportViewModel())
    {
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
