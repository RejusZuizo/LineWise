using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Linewise.Desktop.Views;

public sealed partial class RosterGridView : UserControl
{
    public RosterGridView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
