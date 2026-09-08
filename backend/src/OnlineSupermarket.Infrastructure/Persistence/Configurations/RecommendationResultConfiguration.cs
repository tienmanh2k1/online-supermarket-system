using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Domain.Recommendations;

namespace OnlineSupermarket.Infrastructure.Persistence.Configurations;

internal sealed class RecommendationResultConfiguration : IEntityTypeConfiguration<RecommendationResult>
{
    public void Configure(EntityTypeBuilder<RecommendationResult> builder)
    {
        builder.ToTable("recommendation_results", table =>
        {
            table.HasCheckConstraint("ck_recommendation_results_rank", "`rank` > 0");
            table.HasCheckConstraint("ck_recommendation_results_score", "score + 0 >= 0 AND score + 0 <= 1");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();
        builder.Property(x => x.Scope)
            .HasColumnName("scope")
            .HasMaxLength(30)
            .HasConversion<string>()
            .IsRequired();
        builder.Property(x => x.AudienceKey).HasColumnName("audience_key").HasMaxLength(100).IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("char(36)");
        builder.Property(x => x.SourceProductId).HasColumnName("source_product_id").HasColumnType("char(36)");
        builder.Property(x => x.RecommendedProductId).HasColumnName("recommended_product_id").HasColumnType("char(36)").IsRequired();
        builder.Property(x => x.Score).HasColumnName("score").HasPrecision(12, 6).IsRequired();
        builder.Property(x => x.Rank).HasColumnName("rank").IsRequired();
        builder.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(x => x.AlgorithmVersion).HasColumnName("algorithm_version").HasMaxLength(50).IsRequired();
        builder.Property(x => x.GeneratedAtUtc).HasColumnName("generated_at_utc").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").HasColumnType("datetime(6)").IsRequired();
        builder.Property(x => x.JobRunId).HasColumnName("job_run_id").HasColumnType("char(36)").IsRequired();

        builder.HasIndex(x => new { x.JobRunId, x.AudienceKey, x.RecommendedProductId })
            .IsUnique()
            .HasDatabaseName("ix_recommendation_results_run_audience_product");

        builder.HasOne<BackgroundJobRun>()
            .WithMany()
            .HasForeignKey(x => x.JobRunId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.RecommendedProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(x => x.SourceProductId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
