using Linewise.Domain.Entities;
using Linewise.Domain.Rostering;

namespace Linewise.Application.Printing;

/// <summary>
/// Publishing a week, and saying what it replaced.
/// </summary>
/// <remarks>
/// Publishing is an explicit act. A draft is saved on every keystroke and means nothing to
/// anybody; a published version is the one that goes on a wall, carries a number in its
/// header, and can be told apart from the copy somebody printed on Tuesday.
/// </remarks>
public interface IPublishingService
{
    Task<PublishResult> PublishAsync(
        DateOnly weekStart,
        CancellationToken cancellationToken = default);
}

/// <param name="PreviouslyPublished">
/// What was on the wall before this. Null the first time a week is published, which is
/// exactly when an amendment slip means nothing and a full sheet is the only honest answer.
/// </param>
public sealed record PublishResult(RosterVersion Version, RosterWeek? PreviouslyPublished)
{
    /// <summary>Whether there is a previous sheet for a slip to describe the changes from.</summary>
    public bool CanAmend => PreviouslyPublished is not null;
}
