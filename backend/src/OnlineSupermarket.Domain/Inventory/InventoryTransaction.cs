using OnlineSupermarket.Domain.Common;

namespace OnlineSupermarket.Domain.Inventory;

public sealed class InventoryTransaction : Entity
{
    private InventoryTransaction()
    {
    }

    private InventoryTransaction(
        Guid branchInventoryId,
        InventoryTransactionType transactionType,
        int quantityOnHandDelta,
        int reservedQuantityDelta,
        int quantityOnHandAfter,
        int reservedQuantityAfter,
        InventoryReferenceType referenceType,
        Guid? referenceId,
        string? operationKey,
        Guid? actorUserId,
        string? note,
        DateTime createdAtUtc)
        : base(Guid.NewGuid())
    {
        BranchInventoryId = branchInventoryId;
        TransactionType = transactionType;
        QuantityOnHandDelta = quantityOnHandDelta;
        ReservedQuantityDelta = reservedQuantityDelta;
        QuantityOnHandAfter = quantityOnHandAfter;
        ReservedQuantityAfter = reservedQuantityAfter;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        OperationKey = operationKey;
        ActorUserId = actorUserId;
        Note = note;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BranchInventoryId { get; private set; }
    public InventoryTransactionType TransactionType { get; private set; }
    public int QuantityOnHandDelta { get; private set; }
    public int ReservedQuantityDelta { get; private set; }
    public int QuantityOnHandAfter { get; private set; }
    public int ReservedQuantityAfter { get; private set; }
    public InventoryReferenceType ReferenceType { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public string? OperationKey { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string? Note { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public static InventoryTransaction Create(
        Guid branchInventoryId,
        InventoryTransactionType transactionType,
        int quantityOnHandDelta,
        int reservedQuantityDelta,
        int quantityOnHandAfter,
        int reservedQuantityAfter,
        InventoryReferenceType referenceType,
        Guid? referenceId,
        string? operationKey,
        Guid? actorUserId,
        string? note,
        DateTime createdAtUtc)
    {
if (quantityOnHandAfter < 0 || reservedQuantityAfter < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(quantityOnHandAfter),
                "Transaction snapshots cannot be negative.");
        }

        if (createdAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("createdAtUtc must be UTC.", nameof(createdAtUtc));
        }

        var trimmedKey = operationKey?.Trim();
        ValidateLedgerEquation(transactionType, quantityOnHandDelta, reservedQuantityDelta, quantityOnHandAfter, reservedQuantityAfter);
        return new InventoryTransaction(
            branchInventoryId,
            transactionType,
            quantityOnHandDelta,
            reservedQuantityDelta,
            quantityOnHandAfter,
            reservedQuantityAfter,
            referenceType,
            referenceId,
            string.IsNullOrEmpty(trimmedKey) ? null : trimmedKey,
            actorUserId,
            note?.Trim(),
            createdAtUtc);
    }

    private static void ValidateLedgerEquation(
        InventoryTransactionType transactionType,
        int quantityOnHandDelta,
        int reservedQuantityDelta,
        int quantityOnHandAfter,
        int reservedQuantityAfter)
    {
        // The pre-transaction snapshot reconstructed with (after - delta) must never be negative.
        if (quantityOnHandAfter - quantityOnHandDelta < 0
            || reservedQuantityAfter - reservedQuantityDelta < 0)
        {
            throw new InvalidOperationException(
                "Ledger equation violated: reconstructed pre-transaction snapshot is negative.");
        }

        switch (transactionType)
        {
            case InventoryTransactionType.Reserve:
                if (quantityOnHandDelta != 0 || reservedQuantityDelta <= 0)
                    throw new InvalidOperationException("Reserve must keep quantity on hand unchanged and increase reserved quantity.");
                break;
            case InventoryTransactionType.Release:
                if (quantityOnHandDelta != 0 || reservedQuantityDelta >= 0)
                    throw new InvalidOperationException("Release must keep quantity on hand unchanged and decrease reserved quantity.");
                break;
            case InventoryTransactionType.Sale:
                if (quantityOnHandDelta >= 0 || reservedQuantityDelta >= 0 || quantityOnHandDelta != reservedQuantityDelta)
                    throw new InvalidOperationException("Sale must decrease both balances by the same amount.");
                break;
            case InventoryTransactionType.ManualAdjustment:
                if (reservedQuantityDelta != 0)
                    throw new InvalidOperationException("Manual adjustment must not change reserved quantity.");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(transactionType));
        }
    }
}