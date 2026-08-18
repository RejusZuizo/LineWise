using Avalonia.Controls;
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

/// <summary>
/// Opens each window owned but not modal, and completes when it closes.
/// </summary>
/// <remarks>
/// These used to be modal. A modal window takes the whole application hostage until it is
/// dismissed, and if anything at all goes wrong with dismissing it — the close button ends
/// up somewhere unclickable, the window manager puts it off screen, focus lands somewhere
/// unexpected — the operator is stuck with no route back to the roster. That happened on the
/// development machine twice.
/// <para>
/// Owned rather than free floating, so these still travel with the main window and stay in
/// front of it. What they no longer do is block it: the roster stays readable and clickable
/// while somebody edits a person, which is an improvement on its own and means a window that
/// misbehaves is an annoyance rather than a dead end.
/// </para>
/// <para>
/// Escape still closes them, and the close button is still there. Three ways out, none of
/// which depend on the other two working.
/// </para>
/// </remarks>
public sealed class DialogService : IDialogService
{
    private readonly IServiceProvider _services;

    /// <summary>
    /// One window of each kind at a time. Without this a second click on the sidebar opens
    /// a second People window, and two of them saving the same person is a race nobody
    /// would enjoy debugging.
    /// </summary>
    private readonly Dictionary<Type, Window> _open = [];

    public DialogService(IServiceProvider services) => _services = services;

    public Task ShowLineEditorAsync() =>
        ShowAsync(() => _services.GetRequiredService<LineEditorWindow>());

    public async Task<bool> ShowImportAsync()
    {
        var viewModel = _services.GetRequiredService<ImportViewModel>();
        ImportWindow? opened = null;

        await ShowAsync(() =>
        {
            opened = new ImportWindow(viewModel);
            return opened;
        }).ConfigureAwait(true);

        return viewModel.Committed;
    }

    public Task ShowPeopleAsync() =>
        ShowAsync(() => _services.GetRequiredService<PeopleWindow>());

    /// <summary>
    /// Shows a window and returns a task that completes when it is closed, so a caller can
    /// still redraw afterwards exactly as it did when these were modal.
    /// </summary>
    private Task ShowAsync<TWindow>(Func<TWindow> create)
        where TWindow : Window
    {
        if (_open.TryGetValue(typeof(TWindow), out var existing))
        {
            // Already open. Bring it forward rather than opening a second one, which is
            // what somebody clicking twice actually meant.
            existing.Activate();
            return Task.CompletedTask;
        }

        var window = create();
        var closed = new TaskCompletionSource();

        _open[typeof(TWindow)] = window;

        window.Closed += (_, _) =>
        {
            _open.Remove(typeof(TWindow));
            closed.TrySetResult();
        };

        if (Owner is { } owner)
        {
            window.Show(owner);
        }
        else
        {
            // No main window, which happens in a design time preview and in a test.
            window.Show();
        }

        return closed.Task;
    }

    private static Window? Owner =>
        (Avalonia.Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)
            ?.MainWindow;
}
