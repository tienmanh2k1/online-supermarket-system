using OnlineSupermarket.Domain.Common;

namespace OnlineSupermarket.Domain.Intelligence;

public sealed class DemandForecast : Entity
{
    private DemandForecast()
    {
    }

    private DemandForecast(
        Guid branchInventoryId,
        int horizonDays,
        DateOnly forecastStartDate,
        DateOnly forecastEndDate,
        decimal predictedQuantity,
        int actualDataDays,
        ForecastDataQuality dataQuality,
        string algorithmVersion,
        DateTime generatedAtUtc,
        Guid jobRunId)
        : base(Guid.NewGuid())
    {
        BranchInventoryId = branchInventoryId;
        HorizonDays = horizonDays;
        ForecastStartDate = forecastStartDate;
        ForecastEndDate = forecastEndDate;
        PredictedQuantity = predictedQuantity;
        ActualDataDays = actualDataDays;
        DataQuality = dataQuality;
        AlgorithmVersion = algorithmVersion;
        GeneratedAtUtc = generatedAtUtc;
        JobRunId = jobRunId;
    }

    public Guid BranchInventoryId { get; private set; }
    public int HorizonDays { get; private set; }
    public DateOnly ForecastStartDate { get; private set; }
    public DateOnly ForecastEndDate { get; private set; }
    public decimal PredictedQuantity { get; private set; }
    public int ActualDataDays { get; private set; }
    public ForecastDataQuality DataQuality { get; private set; }
    public string AlgorithmVersion { get; private set; } = string.Empty;
    public DateTime GeneratedAtUtc { get; private set; }
    public Guid JobRunId { get; private set; }

    public static DemandForecast Create(
        Guid branchInventoryId,
        int horizonDays,
        DateOnly forecastStartDate,
        DateOnly forecastEndDate,
        decimal predictedQuantity,
        int actualDataDays,
        ForecastDataQuality dataQuality,
        string algorithmVersion,
        DateTime generatedAtUtc,
        Guid jobRunId)
    {
        if (horizonDays is not (7 or 14))
        {
            throw new ArgumentOutOfRangeException(nameof(horizonDays), "Horizon must be 7 or 14 days.");
        }

        if (predictedQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(predictedQuantity), "Predicted quantity cannot be negative.");
        }

        if (actualDataDays is < 0 or > 28)
        {
            throw new ArgumentOutOfRangeException(nameof(actualDataDays), "Actual data days must be between 0 and 28.");
        }

        if (forecastStartDate > forecastEndDate)
        {
            throw new ArgumentException("Forecast start date cannot be after end date.", nameof(forecastStartDate));
        }

        if (string.IsNullOrWhiteSpace(algorithmVersion))
        {
            throw new ArgumentException("Algorithm version is required.", nameof(algorithmVersion));
        }

        if (generatedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("generatedAtUtc must be UTC.", nameof(generatedAtUtc));
        }

        return new DemandForecast(
            branchInventoryId,
            horizonDays,
            forecastStartDate,
            forecastEndDate,
            predictedQuantity,
            actualDataDays,
            dataQuality,
            algorithmVersion,
            generatedAtUtc,
            jobRunId);
    }
}