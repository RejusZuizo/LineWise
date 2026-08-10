using System.Reflection;
using Linewise.Desktop.Resources;
using Xunit;

namespace Linewise.Tests.Desktop;

/// <summary>
/// Every accessor resolves to a real entry.
/// </summary>
/// <remarks>
/// <see cref="Strings"/> falls back to the key when an entry is missing, so that a missing
/// translation never takes the roster down. The cost of that choice is that a typo is
/// invisible: the screen shows "SentToPrintr" and nothing fails. This is the test that
/// makes it fail.
/// </remarks>
public sealed class StringsTests
{
    public static TheoryData<string> Properties
    {
        get
        {
            var data = new TheoryData<string>();

            foreach (var property in typeof(Strings)
                .GetProperties(BindingFlags.Public | BindingFlags.Static)
                .Where(property => property.PropertyType == typeof(string)))
            {
                data.Add(property.Name);
            }

            return data;
        }
    }

    /// <summary>
    /// Asks the resource table directly rather than comparing the value to the key.
    /// Several entries legitimately equal their own key — "Print" is the word and the name —
    /// so a value-to-key comparison reports a fault that is not there. Found by this test
    /// failing on its first run, which is the correct outcome for a wrong assertion.
    /// </summary>
    [Theory]
    [MemberData(nameof(Properties))]
    public void Every_property_has_an_entry_in_the_resource_file(string name)
    {
        var manager = new System.Resources.ResourceManager(
            "Linewise.Desktop.Resources.Strings",
            typeof(Strings).Assembly);

        var entry = manager.GetString(name, System.Globalization.CultureInfo.InvariantCulture);

        Assert.NotNull(entry);
        Assert.False(string.IsNullOrWhiteSpace(entry));
    }

    [Fact]
    public void There_is_at_least_one_property_to_check()
    {
        // Guards the theory above: a reflection query that stops matching would otherwise
        // pass by testing nothing at all.
        Assert.NotEmpty(Properties);
    }

    [Theory]
    [InlineData(1, "1 error")]
    [InlineData(2, "2 errors")]
    public void Error_counts_use_separate_singular_and_plural_entries(int count, string expected)
    {
        Assert.Equal(expected, Strings.ErrorCount(count));
    }

    [Theory]
    [InlineData(1, "1 notice")]
    [InlineData(3, "3 notices")]
    public void Notice_counts_use_separate_singular_and_plural_entries(int count, string expected)
    {
        Assert.Equal(expected, Strings.NoticeCount(count));
    }

    /// <summary>
    /// The reason singular and plural are separate resources rather than an "s" appended in
    /// code: that trick is English and stops being correct in the first language anybody
    /// asks for.
    /// </summary>
    [Theory]
    [InlineData(1, "short 1 day")]
    [InlineData(4, "short 4 days")]
    public void Short_day_counts_are_not_pluralised_by_adding_a_letter(int days, string expected)
    {
        Assert.Equal(expected, Strings.LineShort(days));
    }

    [Fact]
    public void Formatted_entries_substitute_their_arguments()
    {
        Assert.Equal("3 of 4", Strings.Headcount(3, 4));
        Assert.Contains("7", Strings.StatusPublished(7), StringComparison.Ordinal);
    }

    [Fact]
    public void The_counts_line_carries_all_four_numbers()
    {
        var counts = Strings.Counts(11, 2, 1, 6);

        Assert.Contains("11", counts, StringComparison.Ordinal);
        Assert.Contains("2", counts, StringComparison.Ordinal);
        Assert.Contains("1", counts, StringComparison.Ordinal);
        Assert.Contains("6", counts, StringComparison.Ordinal);
    }
}
