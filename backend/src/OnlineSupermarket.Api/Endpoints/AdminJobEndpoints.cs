using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class AdminJobEndpoints
{
    public static void MapAdminJobEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/jobs")
            .RequireAuthorization("AdminOnly")
            .WithTags("Admin Jobs");

        group.MapGet("/", async (AppDbContext dbContext, [FromQuery] string? jobName = null, [FromQuery] Guid? branchId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default) =>
        {
            var query = dbContext.BackgroundJobRuns.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(jobName))
            {
                query = query.Where(x => x.JobName == jobName);
            }

            if (branchId.HasValue && branchId.Value != Guid.Empty)
            {
                query = query.Where(x => x.BranchId == branchId.Value);
            }

            var totalCount = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderByDescending(x => x.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new JobRunResponse(
                    x.Id,
                    x.JobName,
                    x.Status.ToString(),
                    x.CreatedAtUtc,
                    x.StartedAtUtc,
                    x.CompletedAtUtc,
                    x.ErrorSummary
                ))
                .ToListAsync(cancellationToken);

            return Results.Ok(new PaginatedList<JobRunResponse>(items, totalCount, page, pageSize));
        });

        group.MapGet("/{id:guid}", async (Guid id, AppDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var run = await dbContext.BackgroundJobRuns.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

            if (run == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new JobRunResponse(
                run.Id,
                run.JobName,
                run.Status.ToString(),
                run.CreatedAtUtc,
                run.StartedAtUtc,
                run.CompletedAtUtc,
                run.ErrorSummary
            ));
        });
    }
}
