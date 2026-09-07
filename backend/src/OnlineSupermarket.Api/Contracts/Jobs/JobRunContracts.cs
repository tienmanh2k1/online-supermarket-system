using OnlineSupermarket.Domain.Jobs;

namespace OnlineSupermarket.Api.Contracts.Jobs;

public record JobRunResponse(
    Guid Id,
    string JobName,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? ErrorSummary
);

public record PaginatedList<T>(IReadOnlyCollection<T> Items, int TotalCount, int Page, int PageSize);
