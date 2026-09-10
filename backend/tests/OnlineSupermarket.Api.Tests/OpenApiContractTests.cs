using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace OnlineSupermarket.Api.Tests;

[Collection(ApiConfigurationCollection.Name)]
public sealed class OpenApiContractTests(TestApiFactory factory)
    : IClassFixture<TestApiFactory>
{
    [Theory]
    [InlineData("/api/admin/dashboard/summary")]
    [InlineData("/api/admin/reports/sales")]
    [InlineData("/api/dev/password-reset-emails")]
    [InlineData("/api/auth/me")]
    public async Task GetOpenApi_ProtectedOperationRequiresBearer(string path)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/openapi/v1.json");
        response.EnsureSuccessStatusCode();
        var document = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync());
        var security = document!["paths"]![path]!["get"]!["security"]!.AsArray();
        var requirement = Assert.Single(security)!.AsObject();
        Assert.Single(requirement);
        Assert.True(requirement.ContainsKey("Bearer"));
        Assert.Empty(requirement["Bearer"]!.AsArray());
    }

    [Fact]
    public async Task GetOpenApi_ContainsHealthOperation()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("/api/health", document, StringComparison.Ordinal);
        Assert.Contains("GetHealth", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetOpenApi_InProduction_ReturnsNotFound()
    {
        using var productionFactory = factory.WithWebHostBuilder(builder =>
            builder.UseEnvironment("Production"));
        using var client = productionFactory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOpenApi_ContainsAllAdminCatalogOperationsAndSecurity()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Security scheme
        Assert.Contains("\"Bearer\"", document, StringComparison.Ordinal);
        Assert.Contains("\"scheme\": \"bearer\"", document, StringComparison.OrdinalIgnoreCase);

        // 9 Admin Catalog Paths
        Assert.Contains("/api/admin/catalog/categories", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/categories/{id}", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/categories/{id}/status", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/brands", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/brands/{id}", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/brands/{id}/status", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/products", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/products/{id}", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/catalog/products/{id}/status", document, StringComparison.Ordinal);

        // 12 Operation IDs
        Assert.Contains("AdminGetCategories", document, StringComparison.Ordinal);
        Assert.Contains("AdminCreateCategory", document, StringComparison.Ordinal);
        Assert.Contains("AdminUpdateCategory", document, StringComparison.Ordinal);
        Assert.Contains("AdminUpdateCategoryStatus", document, StringComparison.Ordinal);
        Assert.Contains("AdminGetBrands", document, StringComparison.Ordinal);
        Assert.Contains("AdminCreateBrand", document, StringComparison.Ordinal);
        Assert.Contains("AdminUpdateBrand", document, StringComparison.Ordinal);
        Assert.Contains("AdminUpdateBrandStatus", document, StringComparison.Ordinal);
        Assert.Contains("AdminGetProducts", document, StringComparison.Ordinal);
        Assert.Contains("AdminCreateProduct", document, StringComparison.Ordinal);
        Assert.Contains("AdminUpdateProduct", document, StringComparison.Ordinal);
        Assert.Contains("AdminUpdateProductStatus", document, StringComparison.Ordinal);

        // Status codes
        Assert.Contains("\"200\"", document, StringComparison.Ordinal);
        Assert.Contains("\"201\"", document, StringComparison.Ordinal);
        Assert.Contains("\"400\"", document, StringComparison.Ordinal);
        Assert.Contains("\"401\"", document, StringComparison.Ordinal);
        Assert.Contains("\"403\"", document, StringComparison.Ordinal);
        Assert.Contains("\"404\"", document, StringComparison.Ordinal);
        Assert.Contains("\"409\"", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiDocument_MatchesTrackedDocsApiJson()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        var endpointNode = System.Text.Json.Nodes.JsonNode.Parse(rawJson);

        var docPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../../docs/api/openapi.json"));
        Assert.True(File.Exists(docPath), $"docs/api/openapi.json file must exist at {docPath}");

        var docContent = await File.ReadAllTextAsync(docPath);
        var docNode = System.Text.Json.Nodes.JsonNode.Parse(docContent);

        // Remove servers to ignore port differences
        endpointNode?.AsObject().Remove("servers");
        docNode?.AsObject().Remove("servers");

        Assert.True(
            System.Text.Json.Nodes.JsonNode.DeepEquals(docNode, endpointNode),
            "Generated OpenAPI document does not match tracked docs/api/openapi.json. Please update the file if API changed intentionally.");
    }
}
