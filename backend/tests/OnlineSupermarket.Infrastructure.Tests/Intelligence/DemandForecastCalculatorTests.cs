using OnlineSupermarket.Domain.Intelligence;
using OnlineSupermarket.Infrastructure.Intelligence;

namespace OnlineSupermarket.Infrastructure.Tests.Intelligence;

public sealed class DemandForecastCalculatorTests
{
    private static DateOnly EndDate => new DateOnly(2026, 9, 2);

    private static Dictionary<DateOnly, int> DailySales(int startingOffsetDays, int count, int unitsPerDay)
    {
        var sales = new Dictionary<DateOnly, int>();
        for (var offset = 0; offset < count; offset++)
        {
            sales[EndDate.AddDays(-(startingOffsetDays + offset))] = unitsPerDay;
        }
        return sales;
    }

    [Theory]
    [InlineData(7, 14.0)]
    [InlineData(14, 28.0)]
    public void Calculate_UsesDailyAverageAcrossCompleteCalendarDays(int horizon, double expected)
    {
        var sales = DailySales(0, 7, 2);

        var result = DemandForecastCalculator.Calculate(sales, EndDate, horizon);

        Assert.Equal((decimal)expected, result.PredictedQuantity);
        Assert.Equal(ForecastDataQuality.Partial, result.DataQuality);
        Assert.Equal(7, result.ActualDataDays);
    }

    [Fact]
    public void Calculate_NoHistory_ReturnsZeroInsufficient()
    {
        var result = DemandForecastCalculator.Calculate(new Dictionary<DateOnly, int>(), EndDate, 7);

        Assert.Equal(0m, result.PredictedQuantity);
        Assert.Equal(0, result.ActualDataDays);
        Assert.Equal(ForecastDataQuality.Insufficient, result.DataQuality);
        Assert.Equal(EndDate, result.ForecastStartDate);
        Assert.Equal(EndDate, result.ForecastEndDate);
    }

    [Fact]
    public void Calculate_IncludesZeroSaleCalendarDays_AfterFirstSale()
    {
        var sales = new Dictionary<DateOnly, int>();
        sales[EndDate.AddDays(-6)] = 2;

        var result = DemandForecastCalculator.Calculate(sales, EndDate, 7);

        Assert.Equal(7, result.ActualDataDays);
        Assert.True(result.PredictedQuantity > 1.999999m && result.PredictedQuantity < 2.000001m);
        Assert.Equal(ForecastDataQuality.Partial, result.DataQuality);
        Assert.Equal(EndDate.AddDays(-6), result.ForecastStartDate);
    }

    [Fact]
    public void Calculate_CapsObservationAtTwentyEightCalendarDays()
    {
        var sales = DailySales(0, 60, 1);

        var result = DemandForecastCalculator.Calculate(sales, EndDate, 14);

        Assert.Equal(28, result.ActualDataDays);
        Assert.Equal(EndDate, result.ForecastEndDate);
        Assert.Equal(ForecastDataQuality.Sufficient, result.DataQuality);
        Assert.Equal(14m, result.PredictedQuantity);
    }

    [Fact]
    public void Calculate_SingleObservationDay_IsInsufficient()
    {
        var sales = DailySales(0, 1, 3);

        var result = DemandForecastCalculator.Calculate(sales, EndDate, 7);

        Assert.Equal(1, result.ActualDataDays);
        Assert.Equal(ForecastDataQuality.Insufficient, result.DataQuality);
        Assert.Equal(21m, result.PredictedQuantity);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(30)]
    [InlineData(0)]
    public void Calculate_InvalidHorizon_Throws(int horizon)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            DemandForecastCalculator.Calculate(new Dictionary<DateOnly, int>(), EndDate, horizon));
    }
}