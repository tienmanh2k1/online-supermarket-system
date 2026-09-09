using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Tests.Auth;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class AdminReportingEndpointsTests : IClassFixture<AuthTestApiFactory>
{
    private readonly AuthTestApiFactory _factory;

    public AdminReportingEndpointsTests(AuthTestApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(UserRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var email = $"{Guid.NewGuid()}@example.com";
        var user = User.Create(email, passwordHasher.HashPassword("Password123!"), "Test User", null, role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokenService.GenerateAccessToken(user);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ==========================================
    // AUTHENTICATION & AUTHORIZATION
    // ==========================================

    [Theory]
    [InlineData("/api/admin/dashboard/summary")]
    [InlineData("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09")]
    public async Task Reporting_WithoutToken_ReturnsUnauthorized(string path)
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/admin/dashboard/summary")]
    [InlineData("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09")]
    public async Task Reporting_WithCustomerToken_ReturnsForbidden(string path)
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Customer);
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DashboardSummary_AsAdmin_ReturnsOk()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/dashboard/summary");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SalesReport_AsAdmin_ReturnsOk()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/admin/reports/sales?from=2026-09-01&to=2026-09-09");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
