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
    public void Failed_payment_cannot_be_completed()
    {
        var payment = Payment.Create(Guid.NewGuid(), PaymentMethod.MoMo, 12.50m);
        payment.MarkFailed("declined");

        Assert.Throws<InvalidOperationException>(() => payment.MarkCompleted("txn-1"));
        Assert.Equal(PaymentStatus.Failed, payment.Status);
    }
}
