using OnlineSupermarket.Api.Contracts;
using OnlineSupermarket.Api.Endpoints;
using OnlineSupermarket.Api.Middleware;
using OnlineSupermarket.Infrastructure;
using OnlineSupermarket.Infrastructure.Persistence;
using OnlineSupermarket.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using User = OnlineSupermarket.Domain.Identity.User;
using UserRole = OnlineSupermarket.Domain.Identity.UserRole;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(System.Text.Unicode.UnicodeRanges.All);
});
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddOpenApi(options =>
{
    var bearerScheme = new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "JWT Authorization header using the Bearer scheme."
    };

    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new();
        document.Components.SecuritySchemes ??= new Dictionary<string, Microsoft.OpenApi.IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = bearerScheme;
        return Task.CompletedTask;
    });

    options.AddOperationTransformer((operation, context, cancellationToken) =>
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>().Any()
            && !metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
        {
            operation.Security ??= new List<Microsoft.OpenApi.OpenApiSecurityRequirement>();
            var requirement = new Microsoft.OpenApi.OpenApiSecurityRequirement
            {
                [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", context.Document)] = new List<string>()
            };
            operation.Security.Add(requirement);
        }
        return Task.CompletedTask;
    });
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Apply migrations and seed initial development data
    using var scope = app.Services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

    if (context.Database.IsRelational())
    {
        await context.Database.MigrateAsync();
    }
    await DataSeeder.SeedAllAsync(context, hasher);

    app.MapOpenApi();
    app.MapDevEmailEndpoints();
}

app.UseMiddleware<RequestCancellationMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new HealthResponse("ok")))
    .WithName("GetHealth")
    .WithTags("System");

// Auth
app.MapAuthEndpoints();

// Catalog (A-1, A-2)
app.MapCatalogEndpoints();

// Admin Catalog
app.MapAdminCatalogEndpoints();

// Branch (A-3)
app.MapBranchEndpoints();

// Inventory intelligence (INV)
app.MapInventoryIntelligenceEndpoints();

// Cart (A-4)
app.MapCartEndpoints();

// Checkout + Payment (A-5, C-1, C-2, C-3)
app.MapCheckoutEndpoints();

// Orders (C-4, C-5)
app.MapOrderEndpoints();

// Users (FR-114)
app.MapUserEndpoints();

// Addresses (FR-115)
app.MapAddressEndpoints();

// Promotions (Admin CRUD)
app.MapPromotionEndpoints();

// Reviews (REV-03, REV-04)
app.MapReviewEndpoints();

// Recommendations (VIEW)
app.MapRecommendationEndpoints();

// Admin Jobs
app.MapAdminJobEndpoints();

// Admin Reporting
app.MapAdminReportingEndpoints();

app.Run();

public partial class Program;
