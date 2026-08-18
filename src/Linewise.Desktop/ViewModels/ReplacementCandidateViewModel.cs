using CommunityToolkit.Mvvm.Input;
using Linewise.Application.Rostering;
using Linewise.Desktop.Resources;
using Linewise.Domain.Enums;

namespace Linewise.Desktop.ViewModels;

/// <summary>One name offered to fill a place, and what taking them would cost.</summary>
/// <remarks>
/// The order they arrive in is the answer. This adds only the words: why this person is
/// near the top, and what taking them opens up somewhere else.
/// </remarks>
public sealed partial class ReplacementCandidateViewModel
{
    private readonly RosterCellViewModel _cell;

    public ReplacementCandidateViewModel(ReplacementCandidate candidate, RosterCellViewModel cell)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(cell);

        _cell = cell;

        EmployeeId = candidate.EmployeeId;
        DisplayName = candidate.DisplayName;
        LeavesAHoleElsewhere = candidate.LeavesAHoleElsewhere;

        Reason = candidate switch
        {
            { IsRequiredHere: true } => Strings.CandidateRequiredHere,
            { PreferenceRank: 1 } => Strings.CandidateFirstChoice,
            { PreferenceRank: { } rank } => Strings.CandidateChoice(rank),
            { Status: AvailabilityStatus.Overtime } => Strings.CandidateOnOvertime,
            _ => Strings.CandidateAvailable,
        };

        // Named, not merely flagged. "Taking them leaves Packing short" is something the
        // manager can weigh; "creates a hole elsewhere" is something they have to go and
        // look up.
        Warning = candidate.AlreadyOnLineName is { } line
            ? Strings.CandidateLeavesLineShort(line)
            : string.Empty;
    }

    public Guid EmployeeId { get; }

    public string DisplayName { get; }

    public string Reason { get; }

    public string Warning { get; }

    public bool LeavesAHoleElsewhere { get; }

    [RelayCommand]
    private Task Place() => _cell.PlaceAsync(EmployeeId);
}
