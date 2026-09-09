using Microsoft.AspNetCore.Mvc;
using OnlineSupermarket.Infrastructure.Services;

namespace OnlineSupermarket.Api.Endpoints;

public static class DevEmailEndpoints
{
    public static IEndpointRouteBuilder MapDevEmailEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/dev/password-reset-emails")
            .WithTags("Dev-Email")
            .RequireAuthorization("AdminOnly");

        group.MapGet(string.Empty, GetLatestEmailAsync)
            .WithName("GetLatestDevEmail")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return routes;
    }

    private static async Task<IResult> GetLatestEmailAsync(
        [FromQuery] string? email,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(email))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Email query parameter is required",
                Detail = "Provide email as query parameter"
            });
        }

        // ASP.NET already decodes query parameters, use directly
        var searchEmail = email.Trim();

        var allEmails = DevEmailStore.Instance.GetAll();
        var matching = allEmails
            .Where(e => string.Equals(e.Email, searchEmail, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.CapturedAtUtc)
            .FirstOrDefault();

        if (matching == null)
        {
            return Results.NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "No emails found",
                Detail = $"No password reset emails found for {searchEmail}"
            });
        }

        return Results.Ok(new
        {
            email = matching.Email,
            resetUrl = matching.ResetUrl,
            capturedAtUtc = matching.CapturedAtUtc
        });
    }
}
