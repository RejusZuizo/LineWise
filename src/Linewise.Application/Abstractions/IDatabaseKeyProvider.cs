namespace Linewise.Application.Abstractions;

/// <summary>
/// Supplies the key the database is encrypted with.
/// </summary>
/// <remarks>
/// Behind an interface so the engine and the repositories never see how the key is kept,
/// and so tests can run against a known key without touching the real one. The shipped
/// implementation derives it from a machine and user scoped secret and never writes it to
/// configuration.
/// </remarks>
public interface IDatabaseKeyProvider
{
    /// <summary>
    /// The passphrase for the database, created on first use and stable thereafter. Losing
    /// it means losing the database, which is why the restore procedure is documented.
    /// </summary>
    string GetKey();
}
