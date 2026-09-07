namespace OnlineSupermarket.Infrastructure.Jobs;

public class IntelligenceJobsOptions
{
    public const string SectionName = "IntelligenceJobs";

    public int LeaseMinutes { get; set; } = 10;
    public int MaxConcurrentJobs { get; set; } = 4;
    public int RecommendationIntervalMinutes { get; set; } = 60;
    public int ForecastHourUtc { get; set; } = 1;
}