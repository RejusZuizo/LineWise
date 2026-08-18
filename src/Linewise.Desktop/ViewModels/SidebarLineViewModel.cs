using Linewise.Desktop.Resources;
using Linewise.Domain.Entities;

namespace Linewise.Desktop.ViewModels;

/// <summary>One production line as the sidebar lists it.</summary>
/// <remarks>
/// Deliberately thin. The sidebar answers "what lines exist and what do they want", not
/// "what should they be" — editing lives in its own window, where a mistake can be reviewed
/// before it is saved. Clicking a line is how somebody gets to that window, which is where
/// they were already trying to go.
/// </remarks>
public sealed class SidebarLineViewModel
{
    public SidebarLineViewModel(ProductionLine line)
    {
        ArgumentNullException.ThrowIfNull(line);

        Name = line.Name;
        AccentColour = line.AccentColour;
        Summary = Strings.LineSummary(line.RequiredHeadcount, line.RequiredOperatingAssistants);
        Description = line.Description;
    }

    public string Name { get; }

    public string AccentColour { get; }

    /// <summary>Headcount and assistants, so the sidebar answers the two questions a line
    /// gets asked without opening anything.</summary>
    public string Summary { get; }

    /// <summary>
    /// What the line is for, when somebody has said. Shown under the numbers rather than
    /// instead of them: a description is context, and the headcount is the thing being
    /// scanned for.
    /// </summary>
    public string Description { get; }

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public string EditTip => Strings.EditThisLine;
}
