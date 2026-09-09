using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Microsoft.Extensions.DependencyInjection;
using OnlineSupermarket.Api.Endpoints;
using OnlineSupermarket.Api.Tests.Auth;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Services;

namespace OnlineSupermarket.Api.Tests.Endpoints;

public sealed class DevEmailEndpointsTests : IClassFixture<AuthTestApiFactory>
{
    private readonly AuthTestApiFactory _factory;

    public DevEmailEndpointsTests(AuthTestApiFactory factory)
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

    [Fact]
    public async Task GetLatestEmail_WithoutToken_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var encodedEmail = Uri.EscapeDataString("test@example.com");
        var response = await client.GetAsync($"/api/dev/password-reset-emails?email={encodedEmail}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestEmail_WithCustomerToken_ReturnsForbidden()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Customer);
        var encodedEmail = Uri.EscapeDataString("test@example.com");
        var response = await client.GetAsync($"/api/dev/password-reset-emails?email={encodedEmail}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestEmail_WithAdminToken_NoEmails_ReturnsNotFound()
    {
        DevEmailStore.Instance.Clear();
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var encodedEmail = Uri.EscapeDataString("nonexistent@example.com");
        var response = await client.GetAsync($"/api/dev/password-reset-emails?email={encodedEmail}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetLatestEmail_WithAdminToken_HasEmail_ReturnsLatest()
    {
        DevEmailStore.Instance.Clear();
        var testEmail = "devtest@example.com";

        // Capture an email
        DevEmailStore.Instance.Add(testEmail, "/reset-password?token=abc123");
        await Task.Delay(10); // ensure different timestamps
        DevEmailStore.Instance.Add(testEmail, "/reset-password?token=xyz789");

        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var encodedEmail = Uri.EscapeDataString(testEmail);
        var response = await client.GetAsync($"/api/dev/password-reset-emails?email={encodedEmail}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DevEmailResponse>();
        Assert.NotNull(result);
        Assert.Equal(testEmail, result.Email);
        Assert.Equal("/reset-password?token=xyz789", result.ResetUrl);
    }

    [Fact]
    public async Task GetLatestEmail_MissingEmailQuery_ReturnsBadRequest()
    {
        using var client = await CreateAuthenticatedClientAsync(UserRole.Admin);
        var response = await client.GetAsync("/api/dev/password-reset-emails");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed record DevEmailResponse(string Email, string ResetUrl, DateTime CapturedAtUtc);
}
