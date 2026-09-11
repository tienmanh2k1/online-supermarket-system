using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Api.Contracts.Checkout;
using OnlineSupermarket.Infrastructure.Payments;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Endpoints;

public static class MockPaymentEndpoints
{
    public static IEndpointRouteBuilder MapMockPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/payments/mock").RequireAuthorization().WithTags("Mock-Payment");
        group.MapGet("/{paymentId:guid}", GetAsync);
        group.MapPost("/{paymentId:guid}/complete", CompleteAsync);
        return routes;
    }

    private static async Task<IResult> GetAsync(Guid paymentId, ClaimsPrincipal user, AppDbContext db, CancellationToken ct)
    {
        var payment = await FindOwnedMockPaymentAsync(paymentId, UserId(user), db, ct);
        return payment is null ? Results.NotFound() : Results.Ok(ToDto(payment));
    }

    private static async Task<IResult> CompleteAsync(Guid paymentId, CompleteMockPaymentRequest request, ClaimsPrincipal user, AppDbContext db, IPaymentCallbackProcessor processor, CancellationToken ct)
    {
        if (request.Outcome is not ("Success" or "Failed" or "Cancelled")) return Results.BadRequest();
        var payment = await FindOwnedMockPaymentAsync(paymentId, UserId(user), db, ct);
        if (payment is null) return Results.NotFound();
        var callback = new PaymentCallbackVerificationResult(false, $"mock:{payment.Payment.Id}:{request.Outcome}", payment.Payment.OrderId, payment.Payment.Amount,
            request.Outcome == "Success", JsonSerializer.Serialize(new { mode = "Mock", outcome = request.Outcome }), null, true, payment.Payment.Id);
        var outcome = await processor.ProcessAsync(payment.Payment.Method.ToString(), callback, ct);
        if (outcome == PaymentCallbackOutcome.Conflict) return Results.Conflict();
        db.ChangeTracker.Clear();
        payment = await FindOwnedMockPaymentAsync(paymentId, UserId(user), db, ct);
        return payment is null ? Results.NotFound() : Results.Ok(ToDto(payment));
    }

    private static async Task<PaymentView?> FindOwnedMockPaymentAsync(Guid id, Guid userId, AppDbContext db, CancellationToken ct) =>
        await db.Payments.Where(payment => payment.Id == id && payment.IsMock)
            .Join(db.Orders, payment => payment.OrderId, order => order.Id, (payment, order) => new { Payment = payment, Order = order })
            .Where(item => item.Order.UserId == userId)
            .Select(item => new PaymentView(item.Payment, item.Order))
            .FirstOrDefaultAsync(ct);

    private static MockPaymentDto ToDto(PaymentView value) => new(value.Payment.Id, value.Payment.OrderId, value.Payment.Method.ToString(), value.Payment.Amount, value.Payment.Status.ToString(), value.Order.Status.ToString(), true);
    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? user.FindFirst("sub")!.Value);
    private sealed record PaymentView(OnlineSupermarket.Domain.Payments.Payment Payment, OnlineSupermarket.Domain.Orders.Order Order);
}
