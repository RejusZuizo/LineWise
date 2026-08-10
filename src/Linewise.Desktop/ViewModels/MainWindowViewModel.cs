using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Linewise.Desktop.ViewModels;

/// <summary>
/// The shell's view model. Holds nothing yet beyond what identifies the build.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    public string Title => "Linewise";

    /// <summary>
    /// The informational version, which carries the commit it was built from. On a wall
    /// sheet a stale roster is spotted by its printed timestamp; on screen this is the
    /// equivalent, and it is the first thing worth asking for when somebody reports a fault.
    /// </summary>
    public string Version =>
        typeof(MainWindowViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "unknown version";
}
