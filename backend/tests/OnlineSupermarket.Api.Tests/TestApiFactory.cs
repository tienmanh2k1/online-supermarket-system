using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Services;

namespace OnlineSupermarket.Api.Tests;

public class TestApiFactory : WebApplicationFactory<Program>
{
    public const string TestSecret = "test-webhook-secret";
    private const string VariableName = "ConnectionStrings__DefaultConnection";
    private readonly string? _originalConnectionString;
    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly InMemoryDatabaseRoot _databaseRoot = new();
    private readonly InMemoryCapturingEmailSender _emailSender = new();

    public InMemoryCapturingEmailSender EmailSender => _emailSender;

    public TestApiFactory()
    {
        _originalConnectionString = Environment.GetEnvironmentVariable(VariableName);
        Environment.SetEnvironmentVariable(
            VariableName,
            "Server=localhost;Port=3306;Database=test;User=test;Password=test");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=localhost;Port=3306;Database=test;User=test;Password=test");
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Development");
        builder.UseSetting("Email:UseDevMode", "true");
        builder.UseSetting("Infrastructure:DisableBackgroundServices", "true");
        builder.UseSetting("Payments:Webhooks:VNPay:Secret", TestSecret);
        builder.UseSetting("Payments:Webhooks:MoMo:Secret", TestSecret);
        builder.UseSetting("Payments:Webhooks:MoMo:AccessKey", "test-access-key");

        builder.ConfigureLogging(logging =>
        {
            logging.AddFilter("Microsoft", LogLevel.Warning);
            logging.AddFilter("System", LogLevel.Warning);
        });

        builder.ConfigureServices(services =>
        {
            var efServices = services.Where(d =>
                d.ServiceType.Namespace?.StartsWith("Microsoft.EntityFrameworkCore") == true ||
                d.ServiceType.Namespace?.StartsWith("MySql.EntityFrameworkCore") == true ||
                d.ServiceType == typeof(AppDbContext) ||
                d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                d.ServiceType == typeof(DbContextOptions)).ToList();

            foreach (var descriptor in efServices)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseInMemoryDatabase(_dbName, _databaseRoot);
                options.ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            });
            services.AddSingleton<IDbContextFactory<AppDbContext>>(_ => new ManualDbContextFactory(
                new DbContextOptionsBuilder<AppDbContext>()
                    .UseInMemoryDatabase(_dbName, _databaseRoot)
                    .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                    .Options));

            // Suppress EventLog provider that causes Access Denied on Windows without admin rights
            services.Configure<LoggerFilterOptions>(options =>
            {
                options.Rules.Add(new LoggerFilterRule(
                    "Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider",
                    null,
                    LogLevel.None,
                    null));
            });

            var existingEmailSender = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailSender));
            if (existingEmailSender != null)
            {
                services.Remove(existingEmailSender);
            }
            services.AddSingleton<IEmailSender>(_emailSender);
            services.AddSingleton(_emailSender);
        });
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            Environment.SetEnvironmentVariable(VariableName, _originalConnectionString);
            _emailSender.Clear();
        }
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiConfigurationCollection
{
    public const string Name = "API configuration";
}
