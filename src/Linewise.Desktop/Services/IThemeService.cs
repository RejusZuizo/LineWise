using Avalonia.Styling;

namespace Linewise.Desktop.Services;

/// <summary>Switches between the light and dark palettes.</summary>
/// <remarks>
/// Behind an interface so a view model can offer the choice without referencing
/// <c>Application</c>. Same reason as the dialog service: a view model that reaches into
/// Avalonia cannot be tested without a user interface thread.
/// </remarks>
public interface IThemeService
{
    bool IsDark { get; }

    void Toggle();
}

/// <inheritdoc cref="IThemeService"/>
public sealed class ThemeService : IThemeService
{
    public bool IsDark =>
        Avalonia.Application.Current?.RequestedThemeVariant == ThemeVariant.Dark;

    public void Toggle()
    {
        if (Avalonia.Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = IsDark ? ThemeVariant.Light : ThemeVariant.Dark;
    }
}
