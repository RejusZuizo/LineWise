using Linewise.Desktop.Resources;
using Linewise.Domain.Entities;

namespace Linewise.Desktop.ViewModels;

/// <summary>One production line as the sidebar lists it.</summary>
/// <remarks>
/// Read only and deliberately thin. The sidebar answers "what lines exist and what do they
/// want", not "what should they be" — editing lives in its own window, where a mistake can
/// be reviewed before it is saved.
/// </remarks>
public sealed class SidebarLineViewModel
{
    public SidebarLineViewModel(ProductionLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Name = line.Name;
        AccentColour = line.AccentColour;
        Summary = Strings.LineSummary(line.RequiredHeadcount, line.RequiredOperatingAssistants);
    }

    public string Name { get; }

    public string AccentColour { get; }

    /// <summary>Headcount and assistants, so the sidebar answers the two questions a line
    /// gets asked without opening anything.</summary>
    public string Summary { get; }
}
