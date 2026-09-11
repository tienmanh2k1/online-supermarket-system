using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Payments;

namespace OnlineSupermarket.Infrastructure.Persistence.Configurations;

public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id).HasColumnName("id");
        builder.Property(o => o.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(o => o.BranchId).HasColumnName("branch_id").IsRequired();
        builder.Property(o => o.FulfillmentType).HasColumnName("fulfillment_type").HasMaxLength(20).IsRequired();
        builder.Property(o => o.DeliveryAddressId).HasColumnName("delivery_address_id");
        builder.Property(o => o.RecipientName).HasColumnName("recipient_name").HasMaxLength(100).IsRequired();
        builder.Property(o => o.RecipientPhone).HasColumnName("recipient_phone").HasMaxLength(20).IsRequired();
        builder.Property(o => o.DeliveryAddressSnapshot).HasColumnName("delivery_address_snapshot").HasColumnType("text");
        builder.Property(o => o.Subtotal).HasColumnName("subtotal").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.DiscountAmount).HasColumnName("discount_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.ShippingFee).HasColumnName("shipping_fee").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(o => o.PromotionId).HasColumnName("promotion_id");
        builder.Property(o => o.PromotionCodeSnapshot).HasColumnName("promotion_code_snapshot").HasMaxLength(50);
        builder.Property(o => o.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)");
        builder.Property(o => o.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("datetime(6)");

        builder.HasIndex(o => o.UserId).HasDatabaseName("ix_orders_user_id");
        builder.HasIndex(o => o.Status).HasDatabaseName("ix_orders_status");
        builder.HasIndex(o => o.CreatedAtUtc).HasDatabaseName("ix_orders_created");
    }
}

public sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items");
        builder.HasKey(oi => oi.Id);

        builder.Property(oi => oi.Id).HasColumnName("id");
        builder.Property(oi => oi.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(oi => oi.ProductId).HasColumnName("product_id").IsRequired();
        builder.Property(oi => oi.ProductName).HasColumnName("product_name").HasMaxLength(200).IsRequired();
        builder.Property(oi => oi.Sku).HasColumnName("sku").HasMaxLength(50).IsRequired();
        builder.Property(oi => oi.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(oi => oi.Quantity).HasColumnName("quantity").IsRequired();
        builder.Property(oi => oi.LineTotal).HasColumnName("line_total").HasColumnType("decimal(18,2)").IsRequired();

        builder.HasOne<Order>().WithMany(o => o.Items).HasForeignKey(oi => oi.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(oi => oi.OrderId).HasDatabaseName("ix_order_items_order_id");
    }
}

public sealed class OrderStatusHistoryConfiguration : IEntityTypeConfiguration<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("order_status_histories");
        builder.HasKey(osh => osh.Id);

        builder.Property(osh => osh.Id).HasColumnName("id");
        builder.Property(osh => osh.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(osh => osh.FromStatus).HasColumnName("from_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(osh => osh.ToStatus).HasColumnName("to_status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(osh => osh.Note).HasColumnName("note").HasMaxLength(500);
        builder.Property(osh => osh.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)");

        builder.HasOne<Order>().WithMany(o => o.StatusHistory).HasForeignKey(osh => osh.OrderId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(osh => osh.OrderId).HasDatabaseName("ix_order_status_histories_order_id");
    }
}

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.OrderId).HasColumnName("order_id").IsRequired();
        builder.Property(p => p.Method).HasColumnName("method").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(p => p.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(p => p.IsMock).HasColumnName("is_mock").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.ProviderTransactionId).HasColumnName("provider_transaction_id").HasMaxLength(200);
        builder.Property(p => p.ProviderResponse).HasColumnName("provider_response").HasColumnType("text");
        builder.Property(p => p.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("datetime(6)");
        builder.Property(p => p.CompletedAtUtc).HasColumnName("completed_at_utc").HasColumnType("datetime(6)");

        builder.HasOne<Order>().WithMany().HasForeignKey(p => p.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.OrderId).HasDatabaseName("ix_payments_order_id");
        builder.HasIndex(p => p.ProviderTransactionId).HasDatabaseName("ix_payments_provider_tx_id");
    }
}

public sealed class PaymentCallbackConfiguration : IEntityTypeConfiguration<PaymentCallback>
{
    public void Configure(EntityTypeBuilder<PaymentCallback> builder)
    {
        builder.ToTable("payment_callbacks");
        builder.HasKey(pc => pc.Id);

        builder.Property(pc => pc.Id).HasColumnName("id");
        builder.Property(pc => pc.PaymentId).HasColumnName("payment_id").IsRequired();
        builder.Property(pc => pc.Provider).HasColumnName("provider").HasMaxLength(20).IsRequired();
        builder.Property(pc => pc.ExternalEventId).HasColumnName("external_event_id").HasMaxLength(200).IsRequired();
        builder.Property(pc => pc.RawResponse).HasColumnName("raw_response").HasColumnType("text").IsRequired();
        builder.Property(pc => pc.IsValidSignature).HasColumnName("is_valid_signature").IsRequired();
        builder.Property(pc => pc.Amount).HasColumnName("amount").HasColumnType("decimal(18,2)");
        builder.Property(pc => pc.ResultStatus).HasColumnName("result_status").HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(pc => pc.ReceivedAtUtc).HasColumnName("received_at_utc").HasColumnType("datetime(6)");

        builder.HasOne<Payment>().WithMany().HasForeignKey(pc => pc.PaymentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(pc => new { pc.Provider, pc.ExternalEventId }).HasDatabaseName("ix_payment_callbacks_provider_event").IsUnique();
    }
}
