using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OnlineSupermarket.Infrastructure.Identity;
using OnlineSupermarket.Infrastructure.Inventory;
using OnlineSupermarket.Infrastructure.Intelligence;
using OnlineSupermarket.Infrastructure.Jobs;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Payments;
using OnlineSupermarket.Infrastructure.Recommendations;
using OnlineSupermarket.Infrastructure.Services;

namespace OnlineSupermarket.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("DefaultConnection is required.");
        }

        services.AddDbContext<AppDbContext>(options => options.UseMySQL(connectionString));
        services.AddSingleton<IDbContextFactory<AppDbContext>>(new RuntimeDbContextFactory(connectionString));

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IInventoryMutationService, InventoryMutationService>();
        services.AddScoped<IProductViewEventStore, ProductViewEventStore>();
        services.AddScoped<IBackgroundJobHandler, RecommendationJobHandler>();
        services.AddScoped<IRecurringJobSchedule, RecommendationRecurringSchedule>();
        services.AddScoped<IBackgroundJobHandler, ForecastJobHandler>();
        services.AddScoped<IRecurringJobSchedule, ForecastRecurringSchedule>();

        services.Configure<Jobs.IntelligenceJobsOptions>(
            configuration.GetSection(Jobs.IntelligenceJobsOptions.SectionName));
        services.Configure<VnPayWebhookOptions>(
            configuration.GetSection(VnPayWebhookOptions.SectionName));
        services.Configure<MoMoWebhookOptions>(
            configuration.GetSection(MoMoWebhookOptions.SectionName));
        services.AddScoped<IPaymentCallbackVerifier, VnPayCallbackVerifier>();
        services.AddScoped<IPaymentCallbackVerifier, MomoCallbackVerifier>();
        services.AddScoped<IPaymentCallbackProcessor, PaymentCallbackProcessor>();

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();

        // Register email sender based on environment — DevEmailSender only in Development
        // Use Email:UseDevMode=true to force dev mode (useful for testing)
        var environmentName = configuration["Environment"] ?? configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var useDevEmail = string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase)
            || configuration.GetValue<bool>("Email:UseDevMode");

        if (useDevEmail)
        {
            services.AddSingleton<IEmailSender, DevEmailSender>();
        }
        else
        {
            // Production: fail if email provider is configured but not yet implemented
            var emailProvider = configuration["Email:Provider"];
            if (!string.IsNullOrWhiteSpace(emailProvider))
            {
                throw new InvalidOperationException(
                    $"Email provider '{emailProvider}' is configured but not yet implemented. " +
                    "Set ASPNETCORE_ENVIRONMENT=Development or Email:UseDevMode=true to use the dev email store for local development.");
            }
            throw new InvalidOperationException(
                "Email provider is not configured. Set 'Email:Provider' in configuration " +
                "(e.g., 'SendGrid', 'Ses', 'Smtp') and configure the corresponding API key/credentials. " +
                "Alternatively, set ASPNETCORE_ENVIRONMENT=Development or Email:UseDevMode=true to use the dev email store.");
        }

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

        // Register background services unless explicitly disabled for testing
        if (!configuration.GetValue<bool>("Infrastructure:DisableBackgroundServices"))
        {
            services.AddHostedService<BackgroundServices.RefreshTokenCleanupService>();
            services.AddHostedService<Jobs.IntelligenceWorker>();
            services.AddHostedService<Jobs.RecurringJobScheduler>();
        }

        services.AddSingleton<Jobs.IJobQueue>(sp => new Jobs.ChannelJobQueue());
        services.AddScoped<Jobs.JobRunCoordinator>();
        services.AddScoped<Jobs.JobRunExecutor>();
        services.AddScoped<Jobs.JobLeaseService>();
        services.AddScoped<Jobs.IJobRunStore, Jobs.EfJobRunStore>();

        return services;
    }
}
