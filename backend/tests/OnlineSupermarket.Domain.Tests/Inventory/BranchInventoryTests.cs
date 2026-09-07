using OnlineSupermarket.Domain.Inventory;

namespace OnlineSupermarket.Domain.Tests.Inventory;

public sealed class BranchInventoryTests
{
    [Fact]
    public void Create_WithValidValues_ComputesAvailableQuantity()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 25_000m, 10, 2);

        Assert.Equal(10, inventory.AvailableQuantity);
        Assert.Equal(0, inventory.ReservedQuantity);
    }

    [Theory]
    [InlineData(-1, 10, 2)]
    [InlineData(1000, -1, 2)]
    [InlineData(1000, 10, -1)]
    public void Create_WithNegativeValue_Throws(
        decimal price, int quantity, int reorderLevel)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BranchInventory.Create(
                Guid.NewGuid(), Guid.NewGuid(), price, quantity, reorderLevel));
    }

    [Fact]
    public void Reserve_WhenQuantityIsAvailable_UpdatesAvailableQuantity()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 25_000m, 10, 2);

        inventory.Reserve(3);

        Assert.Equal(3, inventory.ReservedQuantity);
        Assert.Equal(7, inventory.AvailableQuantity);
    }

    [Fact]
    public void Reserve_WhenQuantityExceedsAvailability_Throws()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 25_000m, 2, 1);

        Assert.Throws<InvalidOperationException>(() => inventory.Reserve(3));
    }

    [Fact]
    public void Reserve_WithNonPositiveQuantity_Throws()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 25_000m, 2, 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.Reserve(0));
    }

    [Fact]
    public void Release_Cannot_exceed_reserved_quantity()
    {
        var inventory = BranchInventory.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, 10, 1);
        inventory.Reserve(2);

        Assert.Throws<InvalidOperationException>(() => inventory.Release(3));
        Assert.Equal(2, inventory.ReservedQuantity);
    }

    [Fact]
    public void AdjustQuantity_Cannot_drop_below_reserved_quantity()
    {
        var inventory = BranchInventory.Create(Guid.NewGuid(), Guid.NewGuid(), 10m, 10, 1);
        inventory.Reserve(4);

        Assert.Throws<InvalidOperationException>(() => inventory.AdjustQuantity(3));
        Assert.Equal(10, inventory.QuantityOnHand);
    }

    [Fact]
    public void CompleteSale_DecrementsOnHandAndReservedTogether()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 10m, 100, 20);
        inventory.Reserve(10);

        inventory.CompleteSale(10);

        Assert.Equal(90, inventory.QuantityOnHand);
        Assert.Equal(0, inventory.ReservedQuantity);
        Assert.Equal(90, inventory.AvailableQuantity);
    }

    [Fact]
    public void CompleteSale_WithNonPositiveQuantity_Throws()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 10m, 100, 20);

        Assert.Throws<ArgumentOutOfRangeException>(() => inventory.CompleteSale(0));
    }

    [Fact]
    public void CompleteSale_WhenQuantityExceedsReserved_Throws()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 10m, 100, 20);
        inventory.Reserve(5);

        Assert.Throws<InvalidOperationException>(() => inventory.CompleteSale(10));
    }

    [Fact]
    public void CompleteSale_WhenQuantityExceedsOnHand_Throws()
    {
        var inventory = BranchInventory.Create(
            Guid.NewGuid(), Guid.NewGuid(), 10m, 3, 20);
        inventory.Reserve(3);

        Assert.Throws<InvalidOperationException>(() => inventory.CompleteSale(5));
    }
}
