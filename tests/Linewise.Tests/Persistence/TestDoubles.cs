using Linewise.Application.Abstractions;

namespace Linewise.Tests.Persistence;

/// <summary>A known key, so tests never reach for the developer's protected store.</summary>
internal sealed class FixedDatabaseKeyProvider : IDatabaseKeyProvider
{
    private readonly string _key;

    public FixedDatabaseKeyProvider(string key) => _key = key;

    public string GetKey() => _key;
}

/// <summary>
/// A clock that only moves when a test moves it, so backup filenames and audit timestamps
/// are chosen rather than raced against.
/// </summary>
internal sealed class TestClock : IClock
{
    public DateTime UtcNow { get; private set; } = new(2026, 8, 3, 9, 0, 0, DateTimeKind.Utc);

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}

internal sealed class TestCurrentUser : ICurrentUser
{
    public TestCurrentUser(string name) => Name = name;

    public string Name { get; }
}
