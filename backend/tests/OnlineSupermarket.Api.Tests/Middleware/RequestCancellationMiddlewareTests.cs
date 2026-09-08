using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using OnlineSupermarket.Api.Middleware;
using Xunit;

namespace OnlineSupermarket.Api.Tests.Middleware;

public sealed class RequestCancellationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WhenRequestAborted_CatchesOperationCanceledAndSets499()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var middleware = new RequestCancellationMiddleware(
            _ => throw new OperationCanceledException(cts.Token),
            NullLogger<RequestCancellationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenRequestAbortedWithTaskCanceled_CatchesAndSets499()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var middleware = new RequestCancellationMiddleware(
            _ => throw new TaskCanceledException("Task canceled by caller"),
            NullLogger<RequestCancellationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenTokenNotAborted_RethrowsOperationCanceledException()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource(); // Not canceled

        var middleware = new RequestCancellationMiddleware(
            _ => throw new OperationCanceledException("Internal cancellation"),
            NullLogger<RequestCancellationMiddleware>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenInvalidOperationException_RethrowsEvenIfRequestAborted()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var middleware = new RequestCancellationMiddleware(
            _ => throw new InvalidOperationException("DB error"),
            NullLogger<RequestCancellationMiddleware>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseHasStarted_DoesNotChangeStatusCode()
    {
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var feature = new StartedResponseFeature();
        context.Features.Set<IHttpResponseFeature>(feature);

        var middleware = new RequestCancellationMiddleware(
            _ => throw new OperationCanceledException(cts.Token),
            NullLogger<RequestCancellationMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private sealed class StartedResponseFeature : IHttpResponseFeature
    {
        public int StatusCode { get; set; } = StatusCodes.Status200OK;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => true;
        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }
}
