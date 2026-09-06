using OnlineSupermarket.Domain.Common;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;

namespace OnlineSupermarket.Domain.Inventory;

public sealed class BranchInventory : Entity
{
    private BranchInventory()
    {
    }

    private BranchInventory(
        Guid branchId,
        Guid productId,
        decimal sellingPrice,
        int quantityOnHand,
        int reorderLevel)
        : base(Guid.NewGuid())
    {
        BranchId = branchId;
        ProductId = productId;
        SellingPrice = sellingPrice;
        QuantityOnHand = quantityOnHand;
        ReorderLevel = reorderLevel;
    }

    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal SellingPrice { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int AvailableQuantity => QuantityOnHand - ReservedQuantity;
    public int ReorderLevel { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    // Setters for admin inventory management
    public void AdjustSellingPrice(decimal price)
    {
        if (price < 0) throw new ArgumentOutOfRangeException(nameof(price));
        SellingPrice = price;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AdjustQuantity(int quantityOnHand)
    {
        if (quantityOnHand < 0) throw new ArgumentOutOfRangeException(nameof(quantityOnHand));
        if (quantityOnHand < ReservedQuantity)
            throw new InvalidOperationException("Quantity on hand cannot be below reserved quantity.");
        QuantityOnHand = quantityOnHand;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void AdjustReorderLevel(int level)
    {
        if (level < 0) throw new ArgumentOutOfRangeException(nameof(level));
        ReorderLevel = level;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    // Navigation properties (EF Core managed)
    public Product? Product { get; private set; }
    public Branch? Branch { get; private set; }

    public static BranchInventory Create(
        Guid branchId,
        Guid productId,
        decimal sellingPrice,
        int quantityOnHand,
        int reorderLevel)
    {
        if (sellingPrice < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sellingPrice));
        }

        if (quantityOnHand < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityOnHand));
        }

        if (reorderLevel < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(reorderLevel));
        }

        return new BranchInventory(
            branchId, productId, sellingPrice, quantityOnHand, reorderLevel);
    }

    public void Reserve(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (quantity > AvailableQuantity)
        {
            throw new InvalidOperationException("Insufficient available inventory.");
        }

        ReservedQuantity += quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Release(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (quantity > ReservedQuantity)
            throw new InvalidOperationException("Release exceeds reserved inventory.");
        ReservedQuantity -= quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void CompleteSale(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (quantity > ReservedQuantity || quantity > QuantityOnHand)
        {
            throw new InvalidOperationException("Sale exceeds reserved inventory.");
        }

        QuantityOnHand -= quantity;
        ReservedQuantity -= quantity;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
