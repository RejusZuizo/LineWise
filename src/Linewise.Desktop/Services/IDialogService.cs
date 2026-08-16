using Avalonia.Controls.ApplicationLifetimes;
using Linewise.Desktop.ViewModels;
using Linewise.Desktop.Views;
using Microsoft.Extensions.DependencyInjection;

namespace Linewise.Desktop.Services;

/// <summary>
/// Opens the application's other windows.
/// </summary>
/// <remarks>
/// Behind an interface so a view model can ask for a window without holding one. A view
/// model that knows about <c>Window</c> is a view model that cannot be tested without a
/// user interface thread, and that is how MVVM stops being worth the trouble.
/// </remarks>
public interface IDialogService
{
    Task ShowLineEditorAsync();

    /// <summary>Returns true when something was actually imported, so the caller redraws.</summary>
    Task<bool> ShowImportAsync();

    Task ShowPeopleAsync();
}

/// <inheritdoc cref="IDialogService"/>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;

    public DialogService(IServiceProvider services) => _services = services;

    public async Task ShowLineEditorAsync()
    {
        var window = _services.GetRequiredService<LineEditorWindow>();

        // Modal against the main window when there is one. There is not, during a design
        // time preview or a test, and showing it unowned is better than refusing.
        if (Owner is { } owner)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
            return;
        }

        window.Show();
    }

    public async Task<bool> ShowImportAsync()
    {
        var viewModel = _services.GetRequiredService<ImportViewModel>();
        var window = new ImportWindow(viewModel);

        if (Owner is { } owner)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
        }
        else
        {
            window.Show();
        }

        return viewModel.Committed;
    }

    public async Task ShowPeopleAsync()
    {
        var window = _services.GetRequiredService<PeopleWindow>();

        if (Owner is { } owner)
        {
            await window.ShowDialog(owner).ConfigureAwait(true);
            return;
        }

        window.Show();
    }

    private static Avalonia.Controls.Window? Owner =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
}
