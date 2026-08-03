using Linewise.Application.Import;
using Linewise.Domain.Entities;
using Xunit;

namespace Linewise.Tests.Import;

public sealed class NameMatchingTests
{
    private static readonly Guid AdaId = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid BramId = new("10000000-0000-0000-0000-000000000002");

    [Theory]
    [InlineData("Ada Fictional")]
    [InlineData("  Ada Fictional  ")]
    [InlineData("Ada  Fictional")]
    [InlineData("ADA FICTIONAL")]
    [InlineData("ada fictional")]
    [InlineData("Fictional, Ada")]
    [InlineData("Fictional Ada")]
    public void TheSameNameWrittenDifferentWaysStillMatches(string asWritten)
    {
        var match = MatcherFor(Ada()).Match(asWritten);

        Assert.Equal(NameMatchOutcome.Exact, match.Outcome);
        Assert.Equal(AdaId, match.EmployeeId);
    }

    [Theory]
    [InlineData("Ada Fictionel")]
    [InlineData("Ada Ficitonal")]
    [InlineData("Adah Fictional")]
    public void ATypoIsForgivenButFlagged(string asWritten)
    {
        var match = MatcherFor(Ada()).Match(asWritten);

        Assert.Equal(NameMatchOutcome.Fuzzy, match.Outcome);
        Assert.Equal(AdaId, match.EmployeeId);
    }

    [Fact]
    public void AnAliasMatchesAsWellAsTheFullName()
    {
        var employee = Ada() with { Aliases = ["Ade F"] };

        var match = MatcherFor(employee).Match("Ade F");

        Assert.Equal(NameMatchOutcome.Exact, match.Outcome);
        Assert.Equal(AdaId, match.EmployeeId);
    }

    [Fact]
    public void AccentsAreNotRequiredToMatch()
    {
        var employee = Ada() with { FullName = "Renée Fabricated" };

        var match = MatcherFor(employee).Match("Renee Fabricated");

        Assert.Equal(NameMatchOutcome.Exact, match.Outcome);
    }

    [Fact]
    public void PunctuationDoesNotDecideAMatch()
    {
        var employee = Ada() with { FullName = "Sean O'Invented" };

        var match = MatcherFor(employee).Match("Sean OInvented");

        Assert.Equal(NameMatchOutcome.Exact, match.Outcome);
    }

    [Fact]
    public void TwoPeopleWithTheSameNameAreAQuestionForTheOperator()
    {
        // Picking the first would put somebody on the wrong line without anybody being asked.
        var matcher = new EmployeeNameMatcher(
        [
            Ada(),
            new Employee { Id = BramId, FullName = "Ada Fictional" },
        ]);

        var match = matcher.Match("Ada Fictional");

        Assert.Equal(NameMatchOutcome.Ambiguous, match.Outcome);
        Assert.Null(match.EmployeeId);
        Assert.Equal(2, match.Candidates.Count);
    }

    [Fact]
    public void AnUnknownNameIsUnmatchedRatherThanGuessedAt()
    {
        var match = MatcherFor(Ada()).Match("Quentin Unheardof");

        Assert.Equal(NameMatchOutcome.Unmatched, match.Outcome);
        Assert.Null(match.EmployeeId);
    }

    [Fact]
    public void AShortNameIsNotFuzzyMatched()
    {
        // At three letters almost anything is one edit from anything else, so the tolerance
        // is zero and a near miss stays a miss.
        var matcher = new EmployeeNameMatcher([new Employee { Id = AdaId, FullName = "Jo Ng" }]);

        Assert.Equal(NameMatchOutcome.Unmatched, matcher.Match("Bo Ng").Outcome);
    }

    [Fact]
    public void AnInactiveEmployeeIsNotMatchedAgainst()
    {
        var matcher = new EmployeeNameMatcher([Ada() with { IsActive = false }]);

        Assert.Equal(NameMatchOutcome.Unmatched, matcher.Match("Ada Fictional").Outcome);
    }

    [Fact]
    public void NothingMatchesAnEmptyName()
    {
        Assert.Equal(NameMatchOutcome.Unmatched, MatcherFor(Ada()).Match("   ").Outcome);
        Assert.Equal(NameMatchOutcome.Unmatched, MatcherFor(Ada()).Match(null).Outcome);
    }

    [Fact]
    public void DistanceCountsSingleCharacterEdits()
    {
        Assert.Equal(0, NameMatching.Distance("fictional", "fictional"));
        Assert.Equal(1, NameMatching.Distance("fictional", "fictionel"));
        Assert.Equal(1, NameMatching.Distance("fictional", "fictionl"));
        Assert.Equal(9, NameMatching.Distance("fictional", string.Empty));
    }

    private static Employee Ada() => new() { Id = AdaId, FullName = "Ada Fictional" };

    private static EmployeeNameMatcher MatcherFor(Employee employee) => new([employee]);
}
