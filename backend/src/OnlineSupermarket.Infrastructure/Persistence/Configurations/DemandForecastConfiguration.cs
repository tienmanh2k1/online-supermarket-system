using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Intelligence;

namespace OnlineSupermarket.Infrastructure.Persistence.Configurations;

internal sealed class DemandForecastConfiguration : IEntityTypeConfiguration<DemandForecast>
{
    public void Configure(EntityTypeBuilder<DemandForecast> builder)
    {
        builder.ToTable("demand_forecasts", table =>
        {
            table.HasCheckConstraint("ck_demand_forecasts_horizon", "horizon_days IN (7, 14)");
            table.HasCheckConstraint("ck_demand_forecasts_predicted_quantity", "predicted_quantity + 0 >= 0");
            table.HasCheckConstraint("ck_demand_forecasts_actual_data_days", "actual_data_days + 0 >= 0 AND actual_data_days <= 28");
        });

        builder.HasKey(forecast => forecast.Id);
        builder.Property(forecast => forecast.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(forecast => forecast.BranchInventoryId)
            .HasColumnName("branch_inventory_id").HasColumnType("char(36)").IsRequired();
        builder.Property(forecast => forecast.HorizonDays)
            .HasColumnName("horizon_days").IsRequired();
        builder.Property(forecast => forecast.ForecastStartDate)
            .HasColumnName("forecast_start_date").HasColumnType("date")
            .HasConversion(value => value.ToDateTime(TimeOnly.MinValue), value => DateOnly.FromDateTime(value))
            .IsRequired();
        builder.Property(forecast => forecast.ForecastEndDate)
            .HasColumnName("forecast_end_date").HasColumnType("date")
            .HasConversion(value => value.ToDateTime(TimeOnly.MinValue), value => DateOnly.FromDateTime(value))
            .IsRequired();
        builder.Property(forecast => forecast.PredictedQuantity)
            .HasColumnName("predicted_quantity").HasPrecision(18, 2).IsRequired();
        builder.Property(forecast => forecast.ActualDataDays)
            .HasColumnName("actual_data_days").IsRequired();
        builder.Property(forecast => forecast.DataQuality)
            .HasColumnName("data_quality").HasMaxLength(20).HasConversion<string>().IsRequired();
        builder.Property(forecast => forecast.AlgorithmVersion)
            .HasColumnName("algorithm_version").HasMaxLength(50).IsRequired();
        builder.Property(forecast => forecast.GeneratedAtUtc)
            .HasColumnName("generated_at_utc").HasColumnType("datetime(6)").IsRequired();
        builder.Property(forecast => forecast.JobRunId)
            .HasColumnName("job_run_id").HasColumnType("char(36)").IsRequired();

        builder.HasIndex(forecast => new { forecast.JobRunId, forecast.BranchInventoryId, forecast.HorizonDays })
            .IsUnique()
            .HasDatabaseName("ix_demand_forecasts_run_inventory_horizon");

        builder.HasOne<BranchInventory>()
            .WithMany()
            .HasForeignKey(forecast => forecast.BranchInventoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BackgroundJobRun>()
            .WithMany()
            .HasForeignKey(forecast => forecast.JobRunId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
