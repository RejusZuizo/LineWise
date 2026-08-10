using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Linewise.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Linewise.Desktop;

public sealed partial class App : Avalonia.Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // The last of the three handlers, and the one that matters most: this is the
        // exception raised while the user is doing something, with a window on screen to
        // tell them about it. A stack trace reaching a production manager is a stated
        // non-functional failure, so it is caught here and logged rather than shown.
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Log.Error(e.Exception, "Unhandled exception on the interface thread.");
            e.Handled = true;
        };

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Program.Services.GetRequiredService<MainWindow>();
            Log.Information("Main window created.");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
