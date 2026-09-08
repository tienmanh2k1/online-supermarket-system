using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OnlineSupermarket.Api.Middleware;

public sealed class RequestCancellationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCancellationMiddleware> _logger;

    public RequestCancellationMiddleware(
        RequestDelegate next,
        ILogger<RequestCancellationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogDebug("Request canceled. TraceId={TraceId}", context.TraceIdentifier);
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = 499;
            }
        }
    }
}
