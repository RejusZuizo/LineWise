using System.Globalization;
using System.Text;

namespace Linewise.Application.Import;

/// <summary>
/// Turns the many ways a name gets typed into something comparable.
/// </summary>
/// <remarks>
/// Names on a hand maintained sheet arrive with trailing spaces, doubled spaces, mixed
/// case, the surname first or last, accents dropped, and the occasional transposition. All
/// of that is normal and none of it should force somebody to retype a row.
/// </remarks>
public static class NameMatching
{
    /// <summary>
    /// Lower cased, stripped of accents, with punctuation and runs of whitespace collapsed
    /// to single spaces. This is the form that makes a hyphenated surname match a spaced one.
    /// </summary>
    public static string Normalise(string? name) => Reduce(name, punctuationSeparates: true);

    /// <summary>
    /// As <see cref="Normalise"/>, but punctuation is removed rather than turned into a
    /// space. This is the form that makes O'Invented match OInvented.
    /// </summary>
    /// <remarks>
    /// Both forms are needed and neither is sufficient. Treating an apostrophe as a space
    /// splits a surname in two; removing a hyphen glues two surnames together. Indexing a
    /// name under both costs one extra dictionary entry and settles the argument.
    /// </remarks>
    public static string Compact(string? name) => Reduce(name, punctuationSeparates: false);

    /// <summary>
    /// Normalised, then with the words sorted, so that "Fictional, Ada" and "Ada Fictional"
    /// become the same string.
    /// </summary>
    public static string Sortable(string? name) => SortWords(Normalise(name));

    /// <summary>Compacted, then with the words sorted.</summary>
    public static string SortableCompact(string? name) => SortWords(Compact(name));

    private static string Reduce(string? name, bool punctuationSeparates)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var decomposed = name.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var lastWasSpace = true;

        foreach (var character in decomposed)
        {
            // Drops the accent marks left behind by decomposing, so Renée matches Renee.
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
                lastWasSpace = false;
                continue;
            }

            if (!punctuationSeparates && !char.IsWhiteSpace(character))
            {
                continue;
            }

            if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    private static string SortWords(string reduced)
    {
        var parts = reduced.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Array.Sort(parts, StringComparer.Ordinal);

        return string.Join(' ', parts);
    }

    /// <summary>
    /// Levenshtein distance: how many single character edits separate two strings. Used to
    /// forgive a typo, never to decide a match on its own.
    /// </summary>
    public static int Distance(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        if (left.Length == 0)
        {
            return right.Length;
        }

        if (right.Length == 0)
        {
            return left.Length;
        }

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var column = 0; column <= right.Length; column++)
        {
            previous[column] = column;
        }

        for (var row = 1; row <= left.Length; row++)
        {
            current[0] = row;

            for (var column = 1; column <= right.Length; column++)
            {
                var substitution = previous[column - 1] + (left[row - 1] == right[column - 1] ? 0 : 1);
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[right.Length];
    }

    /// <summary>
    /// How far wrong a name may be and still be offered as a match. Deliberately tight:
    /// a suggestion the operator has to reject is a small cost, but a wrong match that
    /// slips through review puts somebody on a line they are not qualified for.
    /// </summary>
    public static int ToleranceFor(string normalisedName) => normalisedName.Length switch
    {
        < 6 => 0,
        < 12 => 1,
        _ => 2,
    };
}
