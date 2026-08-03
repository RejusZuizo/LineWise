using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Linewise.Infrastructure.Persistence;

/// <summary>
/// Forces every stored instant to UTC on the way in, and stamps the kind back on the way
/// out.
/// </summary>
/// <remarks>
/// SQLite has no time zone concept and hands back a DateTime with an Unspecified kind.
/// Without this, a value saved as UTC comes back looking local, and the next conversion
/// shifts it by the offset. The audit hash would then stop matching after a round trip.
/// </remarks>
internal static class UtcDateTime
{
    public static readonly ValueConverter<DateTime, DateTime> Converter = new(
        value => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime(),
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public static readonly ValueConverter<DateTime?, DateTime?> NullableConverter = new(
        value => value == null
            ? null
            : value.Value.Kind == DateTimeKind.Utc ? value.Value : value.Value.ToUniversalTime(),
        value => value == null
            ? null
            : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc));
}
