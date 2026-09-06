using OnlineSupermarket.Domain.Intelligence;

namespace OnlineSupermarket.Domain.Tests.Intelligence;

public sealed class DemandForecastTests
{
    [Fact]
    public void Create_rejects_undefined_data_quality()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DemandForecast.Create(
            Guid.NewGuid(), 7, DateOnly.FromDateTime(DateTime.UtcNow), DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            1m, 1, (ForecastDataQuality)999, "v1", DateTime.UtcNow, Guid.NewGuid()));
    }
}
