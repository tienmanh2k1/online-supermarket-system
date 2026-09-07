using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Contracts.Jobs;
using OnlineSupermarket.Api.Tests.Auth;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using Xunit;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public class AdminJobEndpointsTests : IClassFixture<AuthTestApiFactory>
{
    private readonly AuthTestApiFactory _factory;

    public AdminJobEndpointsTests(AuthTestApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<OnlineSupermarket.Infrastructure.Identity.IPasswordHasher>();

        var email = $"{Guid.NewGuid()}@example.com";
        var user = User.Create(email, passwordHasher.HashPassword("Password123!"), "Test User", null, role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<OnlineSupermarket.Infrastructure.Identity.ITokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task GetJobs_WhenUnauthorized_ShouldReturn401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/admin/jobs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_WhenNotAdmin_ShouldReturn403()
    {
        var client = await CreateAuthenticatedClientAsync(UserRole.Customer);
        var response = await client.GetAsync("/api/admin/jobs");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_WhenAdmin_ShouldReturnPaginatedJobs()
    {
        var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/jobs?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetJob_WhenNotFound_ShouldReturn404()
    {
        var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync($"/api/admin/jobs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetJobs_FilteredByBranch_ReturnsTerminalRunsByBranchId()
    {
        var client = await CreateAuthenticatedClientAsync(UserRole.Admin);

        var branch = new Branch("Job Branch", "1 Test Street", "0100000000", 10m, 106m);
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Branches.Add(branch);
            await db.SaveChangesAsync();

            var succeeded = new BackgroundJobRun("Forecast", $"branch:{branch.Id}", DateTime.UtcNow.AddHours(-2), branch.Id);
            var succeededToken = Guid.NewGuid().ToString();
            succeeded.Start(succeededToken, DateTime.UtcNow.AddHours(-2).AddSeconds(1), DateTime.UtcNow.AddHours(-1));
            succeeded.MarkAsSucceeded(succeededToken, DateTime.UtcNow.AddHours(-2).AddSeconds(5));
            db.BackgroundJobRuns.Add(succeeded);

            var failed = new BackgroundJobRun("Forecast", $"branch:{branch.Id}", DateTime.UtcNow.AddHours(-3), branch.Id);
            var failedToken = Guid.NewGuid().ToString();
            failed.Start(failedToken, DateTime.UtcNow.AddHours(-3).AddSeconds(1), DateTime.UtcNow.AddHours(-2));
            failed.MarkAsFailed(failedToken, DateTime.UtcNow.AddHours(-3).AddSeconds(2), "boom");
            db.BackgroundJobRuns.Add(failed);

            var other = new BackgroundJobRun("Forecast", "released:other", DateTime.UtcNow.AddHours(-1));
            db.BackgroundJobRuns.Add(other);
            await db.SaveChangesAsync();
        }

        var response = await client.GetAsync(
            $"/api/admin/jobs?jobName=Forecast&branchId={branch.Id}");
        var body = await response.Content.ReadFromJsonAsync<PaginatedList<JobRunResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, body!.TotalCount);
    }
}
