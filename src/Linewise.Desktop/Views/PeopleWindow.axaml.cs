using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Linewise.Desktop.ViewModels;

namespace Linewise.Desktop.Views;

public sealed partial class PeopleWindow : Window
{
    private readonly PeopleViewModel _viewModel;

    public PeopleWindow(PeopleViewModel viewModel)
    {
        _viewModel = viewModel;
        InitializeComponent();
        this.CloseOnEscape();
        DataContext = viewModel;
    }

    /// <summary>Parameterless for the Avalonia designer, which cannot use the container.</summary>
    public PeopleWindow()
        : this(new PeopleViewModel())
    {
    }

    protected override async void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        await _viewModel.LoadAsync().ConfigureAwait(true);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
