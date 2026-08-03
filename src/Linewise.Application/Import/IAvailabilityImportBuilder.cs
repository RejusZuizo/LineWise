using Linewise.Domain.Entities;

namespace Linewise.Application.Import;

/// <summary>
/// Turns a sheet that has been read into an import ready for review.
/// </summary>
/// <remarks>
/// Pure, like the assignment engine, and for the same reason: this is where the rules about
/// what a sheet means live, and rules that touch nothing can be tested against a handful of
/// objects instead of against a folder of spreadsheets.
/// </remarks>
public interface IAvailabilityImportBuilder
{
    AvailabilityImportResult Build(
        RawSheet sheet,
        ImportTemplate template,
        IReadOnlyList<Employee> employees);
}
