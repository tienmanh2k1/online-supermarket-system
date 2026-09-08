using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Jobs;

namespace OnlineSupermarket.Infrastructure.Persistence.Configurations;

public class BackgroundJobRunConfiguration : IEntityTypeConfiguration<BackgroundJobRun>
{
    public void Configure(EntityTypeBuilder<BackgroundJobRun> builder)
    {
        builder.ToTable("background_job_runs");

        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.JobName)
            .HasColumnName("job_name")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LockKey)
            .HasColumnName("lock_key")
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasColumnType("char(36)");

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc");

        builder.Property(x => x.StartedAtUtc)
            .HasColumnName("started_at_utc");

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnName("completed_at_utc");

        builder.Property(x => x.ErrorSummary)
            .HasColumnName("error_summary")
            .HasMaxLength(1000);

        builder.Property(x => x.LockToken)
            .HasColumnName("lock_token")
            .HasMaxLength(50);

        builder.Property(x => x.LeaseExpiresAtUtc)
            .HasColumnName("lease_expires_at_utc");

        builder.HasIndex(x => new { x.JobName, x.LockKey })
            .IsUnique();

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
