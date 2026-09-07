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

        builder.Property(x => x.JobName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.LockKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.BranchId)
            .HasColumnName("branch_id")
            .HasColumnType("char(36)");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.LockToken)
            .HasMaxLength(50);

        builder.Property(x => x.ErrorSummary)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.JobName, x.LockKey })
            .IsUnique();

        builder.HasOne<Branch>()
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
