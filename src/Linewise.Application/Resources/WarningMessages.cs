using System.Globalization;
using System.Resources;
using Linewise.Domain.Enums;

namespace Linewise.Application.Resources;

/// <summary>
/// Looks up the display text for a warning. Text lives in a resource file, never inline
/// in the code that raises the warning.
/// </summary>
internal static class WarningMessages
{
    private static readonly ResourceManager Manager =
        new("Linewise.Application.Resources.EngineWarnings", typeof(WarningMessages).Assembly);

    /// <summary>
    /// Resolves the message for a code and fills in its placeholders. Falls back to the
    /// code name if the resource is missing, because a missing translation must never
    /// take the roster down.
    /// </summary>
    public static string Format(WarningCode code, params object?[] arguments)
    {
        var key = code.ToString();
        var template = Manager.GetString(key, CultureInfo.CurrentUICulture);

        if (string.IsNullOrEmpty(template))
        {
            return key;
        }

        return arguments.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentCulture, template, arguments);
    }
}
