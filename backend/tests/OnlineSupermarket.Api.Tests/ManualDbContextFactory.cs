using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests;

public sealed class ManualDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext() => new(options);
    public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken) => Task.FromResult(CreateDbContext());
}