using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TourLab.Azure.Infrastructure.Persistence;

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => RequireUtc(value), value => DateTime.SpecifyKind(value, DateTimeKind.Utc))
{
    private static DateTime RequireUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : throw new ArgumentException("UTC DateTime required.");
}
