using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
namespace TourLab.Azure.Infrastructure.Persistence;
public sealed class UtcOffsetConverter() : ValueConverter<DateTimeOffset, DateTime>(
    value => value.UtcDateTime, value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));