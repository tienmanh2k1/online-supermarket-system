using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineSupermarket.Api.Tests;

namespace OnlineSupermarket.Api.Tests.Configuration;

[Collection(OnlineSupermarket.Api.Tests.ApiConfigurationCollection.Name)]
public sealed class PaymentModeTests
{
    [Fact]
    public void Mock_Mode_IsAllowed_InDevelopment()
    {
        using var factory = CreateFactory("Development", "Mock");

        using var client = factory.CreateClient();

        Assert.NotNull(client);
    }

    [Fact]
    public void Mock_Mode_IsRejected_OutsideDevelopment()
    {
        using var factory = CreateFactory("Production", "Mock");

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Payments:Mode 'Mock' is only allowed in Development.", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Unknown_Payment_Mode_IsRejected()
    {
        using var factory = CreateFactory("Development", "Live");

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Payments:Mode must be either 'Mock' or 'Sandbox'.", exception.ToString(), StringComparison.Ordinal);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment, string mode) =>
        new TestApiFactory()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(environment);
                builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Port=3306;Database=test;User=test;Password=test");
                builder.UseSetting("Email:UseDevMode", "true");
                builder.UseSetting("Infrastructure:DisableBackgroundServices", "true");
                builder.UseSetting("Payments:Mode", mode);
            });
}
