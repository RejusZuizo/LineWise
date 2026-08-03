using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Linewise.Infrastructure.Persistence;

/// <summary>
/// Used only by the EF tooling when scaffolding a migration.
/// </summary>
/// <remarks>
/// A migration is schema, not data, so this deliberately does not go anywhere near the real
/// database path or the real key. Nothing here ever runs in the shipped application.
/// </remarks>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<LinewiseDbContext>
{
    public LinewiseDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<LinewiseDbContext>()
            .UseSqlite("Data Source=linewise-design-time.db")
            .Options;

        return new LinewiseDbContext(options);
    }
}
