using OnlineSupermarket.Domain.Intelligence;

namespace OnlineSupermarket.Infrastructure.Intelligence;

public sealed record ForecastCalculation(
    int HorizonDays,
    DateOnly ForecastStartDate,
    DateOnly ForecastEndDate,
    decimal PredictedQuantity,
    int ActualDataDays,
    ForecastDataQuality DataQuality);

public static class DemandForecastCalculator
{
    private const int MaxObservationDays = 28;
    public const int ShortHorizonDays = 7;
    public const int LongHorizonDays = 14;

    public static ForecastCalculation Calculate(
        IReadOnlyDictionary<DateOnly, int> dailySales,
        DateOnly observationEnd,
        int horizonDays)
    {
        EnsureHorizon(horizonDays);

        var saleDays = dailySales
            .Where(entry => entry.Key <= observationEnd && entry.Value > 0)
            .Select(entry => entry.Key)
            .ToList();

        if (saleDays.Count == 0)
        {
            return new ForecastCalculation(
                horizonDays, observationEnd, observationEnd,
                0m, 0, ForecastDataQuality.Insufficient);
        }

        var firstSaleDay = saleDays.Min();
        var windowStart = MaxDate(firstSaleDay, observationEnd.AddDays(-(MaxObservationDays - 1)));
        var actualDataDays = observationEnd.DayNumber - windowStart.DayNumber + 1;

        decimal total = 0m;
        for (var day = windowStart; day <= observationEnd; day = day.AddDays(1))
        {
            total += (dailySales.TryGetValue(day, out var quantity) && quantity > 0)
                ? quantity
                : 0;
        }

        var average = total / actualDataDays;
        var predicted = average * horizonDays;

        ForecastDataQuality quality;
        if (actualDataDays < 7)
        {
            quality = ForecastDataQuality.Insufficient;
        }
        else if (actualDataDays <= 13)
        {
            quality = ForecastDataQuality.Partial;
        }
        else
        {
            quality = ForecastDataQuality.Sufficient;
        }

        return new ForecastCalculation(
            horizonDays, windowStart, observationEnd,
            predicted, actualDataDays, quality);
    }

    private static void EnsureHorizon(int horizonDays)
    {
        if (horizonDays is not (ShortHorizonDays or LongHorizonDays))
        {
            throw new ArgumentOutOfRangeException(nameof(horizonDays), "Horizon must be 7 or 14 days.");
        }
    }

    private static DateOnly MaxDate(DateOnly left, DateOnly right)
        => left.DayNumber > right.DayNumber ? left : right;
}