using Linewise.Application.Persistence;

namespace Linewise.Application.Rostering;

/// <summary>
/// Gathers a week's inputs, runs the engine, and stores the result as the draft.
/// </summary>
/// <remarks>
/// The engine reads no repository and opens no file, which is what makes it reproducible.
/// Something has to do the reading, and this is it. Keeping that here rather than in a view
/// model means the order the inputs are assembled in is testable, and means a future web
/// front end gets the same behaviour rather than a second implementation of it.
/// </remarks>
public interface IRosterGenerationService
{
    /// <summary>
    /// Generates the week beginning <paramref name="weekStart"/> and saves it as the draft,
    /// replacing any existing draft for that week.
    /// </summary>
    Task<StoredRoster> GenerateAsync(DateOnly weekStart, CancellationToken cancellationToken = default);
}
