using System.Globalization;

namespace Linewise.Tests.Builders;

/// <summary>
/// Predictable identifiers. Ordering by identifier is the engine's last tie break, so
/// tests need to know which of two otherwise equal people sorts first. These ascend in
/// creation order, which makes "the first one declared wins" a readable expectation.
/// </summary>
internal static class TestIds
{
    public static Guid Employee(int index) => Make(1, index);

    public static Guid Line(int index) => Make(2, index);

    public static Guid Skill(int index) => Make(3, index);

    public static Guid Shift(int index) => Make(4, index);

    private static Guid Make(int kind, int index) =>
        Guid.ParseExact(
            string.Create(CultureInfo.InvariantCulture, $"{kind:D8}-0000-0000-0000-{index:D12}"),
            "d");
}
