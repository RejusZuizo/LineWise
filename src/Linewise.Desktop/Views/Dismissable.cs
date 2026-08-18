using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Linewise.Desktop.Views;

/// <summary>
/// Escape closes a window. Attached by every dialog in the application.
/// </summary>
/// <remarks>
/// Every dialog had exactly one way out: a Close button, reachable only with the mouse. That
/// is fine until the window manager puts the window somewhere the button cannot be clicked,
/// and then the application has trapped its operator with no way back to the roster. A
/// modal with no keyboard escape is a modal that can strand somebody.
/// <para>
/// One place rather than an override in each code-behind, so a dialog added later gets the
/// behaviour by asking for it rather than by somebody remembering the pattern.
/// </para>
/// <para>
/// Tunnelling rather than bubbling. A dialog holding a <see cref="TextBox"/> would otherwise
/// see the key handled before it arrives, and the Import and People windows both hold one.
/// </para>
/// </remarks>
internal static class Dismissable
{
    public static void CloseOnEscape(this Window window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || sender is not Window window)
        {
            return;
        }

        e.Handled = true;
        window.Close();
    }
}
