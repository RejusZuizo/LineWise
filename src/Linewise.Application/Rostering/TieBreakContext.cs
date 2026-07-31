namespace Linewise.Application.Rostering;

/// <summary>What a tie break gets to look at when it decides who goes first.</summary>
public sealed record TieBreakContext
{
    public required DateOnly Date { get; init; }

    public required Guid LineId { get; init; }

    public required IAssignmentLedger Ledger { get; init; }
}
