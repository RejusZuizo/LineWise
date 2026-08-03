using Linewise.Application.Printing;
using Xunit;

namespace Linewise.Tests.Printing;

public sealed class ExportSanitiserTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("+44 7700 900000")]
    [InlineData("-Fictional")]
    [InlineData("@Invented")]
    [InlineData("=cmd|' /c calc'!A1")]
    public void AValueASpreadsheetWouldExecuteIsDefused(string value)
    {
        var sanitised = ExportSanitiser.Sanitise(value);

        Assert.StartsWith("'", sanitised, StringComparison.Ordinal);
        Assert.EndsWith(value, sanitised, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("  =1+1")]
    [InlineData("\t@Invented")]
    public void LeadingWhitespaceDoesNotGetYouPast(string value)
    {
        // Spreadsheets trim before deciding whether something is a formula, so the check has
        // to trim too.
        Assert.StartsWith("'", ExportSanitiser.Sanitise(value), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Ada Fictional")]
    [InlineData("Pastry")]
    [InlineData("O'Invented")]
    [InlineData("")]
    public void AnOrdinaryValueIsLeftAlone(string value)
    {
        Assert.Equal(value, ExportSanitiser.Sanitise(value));
    }

    [Fact]
    public void NullBecomesEmptyRatherThanThrowing()
    {
        Assert.Equal(string.Empty, ExportSanitiser.Sanitise(null));
    }
}
