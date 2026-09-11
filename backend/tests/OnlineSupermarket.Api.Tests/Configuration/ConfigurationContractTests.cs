using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OnlineSupermarket.Infrastructure;

namespace OnlineSupermarket.Api.Tests.Configuration;

[Collection(OnlineSupermarket.Api.Tests.ApiConfigurationCollection.Name)]
public sealed class ConfigurationContractTests
{
    [Fact]
    public void MissingDefaultConnection_StopsStartup()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddInfrastructure(configuration, new DevelopmentHostEnvironment()));

        Assert.Equal("DefaultConnection is required.", exception.Message);
    }

    [Fact]
    public void MissingDefaultConnection_StopsApiCompositionRoot()
    {
        const string variableName = "ConnectionStrings__DefaultConnection";
        var originalValue = Environment.GetEnvironmentVariable(variableName);
        Environment.SetEnvironmentVariable(variableName, null);

        try
        {
            using var factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.UseSetting("ConnectionStrings:DefaultConnection", "");
                });

            var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

            Assert.Contains("DefaultConnection is required.", exception.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variableName, originalValue);
        }
    }
}

file sealed class DevelopmentHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = nameof(OnlineSupermarket);
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
