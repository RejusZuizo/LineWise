using Linewise.Domain.Entities;
using Linewise.Infrastructure.Logging;
using Serilog;
using Serilog.Events;
using Xunit;

namespace Linewise.Tests.Logging;

/// <summary>
/// Threat T6: personal data must not reach a log file.
/// </summary>
/// <remarks>
/// Written against a real logger with a real destructuring policy rather than by calling
/// the policy directly. What matters is what Serilog ends up writing, and a policy that is
/// correct but never invoked would pass the other kind of test.
/// </remarks>
public sealed class RedactionTests
{
    private static readonly Employee Somebody = new()
    {
        Id = Guid.Parse("8f14e45f-ce0a-4d0b-9f4e-1a2b3c4d5e6f"),
        FullName = "Ada Fictional",
        Aliases = ["Fictional, Ada", "A. Fictional"],
        IsTemporary = true,
    };

    [Fact]
    public void An_employee_logged_as_a_structure_loses_their_name()
    {
        var written = Capture(log => log.Information("Assigned {@Employee}", Somebody));

        Assert.DoesNotContain("Ada Fictional", written, StringComparison.Ordinal);
        Assert.Contains(PersonalDataDestructuringPolicy.Redacted, written, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aliases are how somebody appears on the factory's spreadsheet, which makes them more
    /// identifying than the formal name rather than less.
    /// </summary>
    [Fact]
    public void Aliases_are_not_written_either()
    {
        var written = Capture(log => log.Information("Matched {@Employee}", Somebody));

        Assert.DoesNotContain("Fictional, Ada", written, StringComparison.Ordinal);
        Assert.DoesNotContain("A. Fictional", written, StringComparison.Ordinal);
    }

    /// <summary>
    /// The identifier survives on purpose. A log that cannot say which employee it means is
    /// not much of a log, and a Guid is only personal data to somebody holding the database.
    /// </summary>
    [Fact]
    public void The_identifier_survives()
    {
        var written = Capture(log => log.Information("Assigned {@Employee}", Somebody));

        Assert.Contains("8f14e45f", written, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The honest limit of this control, asserted so that it is a known gap rather than an
    /// assumed one. A name interpolated into a message arrives as an opaque string and no
    /// destructuring policy can reach inside it. This is a rule about how logging calls are
    /// written, and the test exists to stop anybody believing otherwise.
    /// </summary>
    [Fact]
    public void A_name_interpolated_into_a_message_is_not_caught()
    {
        var written = Capture(log => log.Information("Assigned " + Somebody.FullName));

        Assert.Contains("Ada Fictional", written, StringComparison.Ordinal);
    }

    private static string Capture(Action<ILogger> write)
    {
        var sink = new CapturingSink();

        using var logger = new LoggerConfiguration()
            .Destructure.With<PersonalDataDestructuringPolicy>()
            .WriteTo.Sink(sink)
            .CreateLogger();

        write(logger);

        return sink.Rendered;
    }

    private sealed class CapturingSink : Serilog.Core.ILogEventSink
    {
        private readonly System.Text.StringBuilder _written = new();

        public string Rendered => _written.ToString();

        public void Emit(LogEvent logEvent)
        {
            using var writer = new StringWriter();
            logEvent.RenderMessage(writer);
            _written.AppendLine(writer.ToString());
        }
    }
}
