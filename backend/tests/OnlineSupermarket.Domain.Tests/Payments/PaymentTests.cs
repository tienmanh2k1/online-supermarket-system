using OnlineSupermarket.Domain.Payments;

namespace OnlineSupermarket.Domain.Tests.Payments;

public sealed class PaymentTests
{
    [Fact]
    public void Pending_can_be_completed_once()
    {
        var payment = Payment.Create(Guid.NewGuid(), PaymentMethod.VNPay, 12.50m);

        payment.MarkCompleted("txn-1", "ok");

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal("txn-1", payment.ProviderTransactionId);
    }

    [Fact]
    public void Terminal_payment_cannot_be_resurrected_or_reversed()
    {
        var payment = Payment.Create(Guid.NewGuid(), PaymentMethod.VNPay, 12.50m);
        payment.MarkCompleted("txn-1");

        Assert.Throws<InvalidOperationException>(() => payment.MarkCompleted("txn-2"));
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("late failure"));
        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal("txn-1", payment.ProviderTransactionId);
    }

    [Fact]
    public void Pending_can_fail_once()
    {
        var payment = Payment.Create(Guid.NewGuid(), PaymentMethod.VNPay, 12.50m);

        payment.MarkFailed("declined");

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("declined", payment.ProviderResponse);
    }

    [Fact]
    public void Completed_payment_rejects_later_transitions()
    {
        var payment = Payment.Create(Guid.NewGuid(), PaymentMethod.VNPay, 12.50m);
        var before = DateTime.UtcNow.AddMinutes(-5);
        payment.MarkCompleted("txn-1", "ok");

        Assert.Throws<InvalidOperationException>(() => payment.MarkCompleted("txn-2", "again"));
        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("late failure"));

        Assert.Equal(PaymentStatus.Completed, payment.Status);
        Assert.Equal("txn-1", payment.ProviderTransactionId);
        Assert.Equal("ok", payment.ProviderResponse);
        Assert.NotNull(payment.CompletedAtUtc);
        Assert.True(payment.CompletedAtUtc >= before);
    }

    [Fact]
    public void Failed_payment_rejects_later_transitions_and_keeps_tracking()
    {
        var payment = Payment.Create(Guid.NewGuid(), PaymentMethod.MoMo, 12.50m);
        payment.MarkFailed("declined");

        Assert.Throws<InvalidOperationException>(() => payment.MarkFailed("again"));
        Assert.Throws<InvalidOperationException>(() => payment.MarkCompleted("txn-1"));

        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal("declined", payment.ProviderResponse);
        Assert.Null(payment.ProviderTransactionId);
        Assert.Null(payment.CompletedAtUtc);
    }

    [Fact]
    public void Cod_pending_collection_can_be_completed_or_failed()
    {
        var completed = Payment.Create(Guid.NewGuid(), PaymentMethod.COD, 100m);
        Assert.Equal(PaymentStatus.PendingCollection, completed.Status);

        completed.MarkCompleted("cod-cash-receipt", "collected");

        Assert.Equal(PaymentStatus.Completed, completed.Status);
        Assert.Equal("cod-cash-receipt", completed.ProviderTransactionId);

        var failed = Payment.Create(Guid.NewGuid(), PaymentMethod.COD, 100m);
        failed.MarkFailed("customer declined");

        Assert.Equal(PaymentStatus.Failed, failed.Status);
    }

    [Fact]
    public void Processing_can_be_completed_or_failed()
    {
        var completed = Payment.Create(Guid.NewGuid(), PaymentMethod.VNPay, 50m);
        ForceProcessing(completed);

        completed.MarkCompleted("gateway-ok", "confirmed");

        Assert.Equal(PaymentStatus.Completed, completed.Status);

        var failed = Payment.Create(Guid.NewGuid(), PaymentMethod.MoMo, 50m);
        ForceProcessing(failed);

        failed.MarkFailed("timeout");

        Assert.Equal(PaymentStatus.Failed, failed.Status);
    }

    private static void ForceProcessing(Payment payment)
    {
        var statusProperty = typeof(Payment).GetProperty(nameof(Payment.Status))!;
        statusProperty.SetValue(payment, PaymentStatus.Processing);
        Assert.Equal(PaymentStatus.Processing, payment.Status);
    }
}
