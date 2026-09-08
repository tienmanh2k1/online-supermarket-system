using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Infrastructure.Inventory;

public sealed class InventoryMutationService(
    AppDbContext dbContext,
    TimeProvider timeProvider)
    : IInventoryMutationService
{
    public async Task ApplyBatchAsync(
        IReadOnlyCollection<InventoryMutationCommand> commands,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational()
            && dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Inventory mutations require an active database transaction.");
        }

        if (commands.Count == 0)
        {
            return;
        }

        var distinctIds = commands
            .Select(c => c.BranchInventoryId)
            .Distinct()
            .OrderBy(id => id)
            .ToArray();

        // MySql.EntityFrameworkCore throws an NRE binding a multi-element parameterized
        // Contains(Guid[]) to an IN clause (single-element collapses to scalar equality and works).
        // Load each inventory by id so no bound collection reaches the provider.
        var inventories = new List<BranchInventory>(distinctIds.Length);
        foreach (var id in distinctIds)
        {
            var inventory = await dbContext.BranchInventories
                .OrderBy(bi => bi.Id)
                .FirstOrDefaultAsync(bi => bi.Id == id, cancellationToken);
            if (inventory is null)
            {
                throw new InvalidOperationException($"Inventory {id} not found.");
            }
            inventories.Add(inventory);
        }

        var inventoryMap = inventories.ToDictionary(bi => bi.Id);

        var commandsByInventory = commands
            .Select((command, index) => (Command: command, Index: index))
            .GroupBy(x => x.Command.BranchInventoryId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(x => x.Index).Select(x => x.Command).ToArray());

        var operationKeys = commands
            .Where(c => !string.IsNullOrWhiteSpace(c.OperationKey))
            .Select(c => c.OperationKey!.Trim())
            .Distinct()
            .ToArray();

        var replayable = new Dictionary<string, InventoryTransaction>();
        if (operationKeys.Length > 0)
        {
            // Avoid parameterized string-Contains which the provider fails to bind for 2+ keys.
            foreach (var key in operationKeys)
            {
                var existing = await dbContext.InventoryTransactions
                    .FirstOrDefaultAsync(t => t.OperationKey == key, cancellationToken);
                if (existing is not null)
                {
                    replayable[key] = existing;
                }
            }
        }

        var occurredAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        var toApply = new List<InventoryMutationCommand>();

        foreach (var inventoryId in distinctIds)
        {
            foreach (var command in commandsByInventory[inventoryId])
            {
                var key = string.IsNullOrWhiteSpace(command.OperationKey)
                    ? null
                    : command.OperationKey.Trim();

                if (key is not null && replayable.TryGetValue(key, out var existing))
                {
                    if (existing.TransactionType != command.TransactionType ||
                        existing.ReferenceType != command.ReferenceType ||
                        existing.ReferenceId != command.ReferenceId ||
                        existing.BranchInventoryId != command.BranchInventoryId)
                    {
                        throw new InvalidOperationException(
                            $"Operation key '{key}' was replayed with mismatched command.");
                    }

                    continue;
                }

                Preflight(command, inventoryMap[inventoryId]);
                toApply.Add(command);
            }
        }

        foreach (var command in toApply)
        {
            var inventory = inventoryMap[command.BranchInventoryId];
            var beforeOnHand = inventory.QuantityOnHand;

            int onHandDelta;
            int reservedDelta;
            switch (command.TransactionType)
            {
                case InventoryTransactionType.Reserve:
                    onHandDelta = 0;
                    reservedDelta = command.Quantity;
                    inventory.Reserve(command.Quantity);
                    break;

                case InventoryTransactionType.Release:
                    onHandDelta = 0;
                    reservedDelta = -command.Quantity;
                    inventory.Release(command.Quantity);
                    break;

                case InventoryTransactionType.Sale:
                    onHandDelta = -command.Quantity;
                    reservedDelta = -command.Quantity;
                    inventory.CompleteSale(command.Quantity);
                    break;

                case InventoryTransactionType.ManualAdjustment:
                    onHandDelta = command.AbsoluteQuantityOnHand!.Value - beforeOnHand;
                    reservedDelta = 0;
                    inventory.AdjustQuantity(command.AbsoluteQuantityOnHand!.Value);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(command.TransactionType));
            }

            dbContext.InventoryTransactions.Add(InventoryTransaction.Create(
                command.BranchInventoryId,
                command.TransactionType,
                onHandDelta,
                reservedDelta,
                inventory.QuantityOnHand,
                inventory.ReservedQuantity,
                command.ReferenceType,
                command.ReferenceId,
                command.OperationKey?.Trim(),
                command.ActorUserId,
                command.Note,
                command.OccurredAtUtc ?? occurredAtUtc));
        }
    }

    private static void Preflight(InventoryMutationCommand command, BranchInventory inventory)
    {
        switch (command.TransactionType)
        {
            case InventoryTransactionType.Reserve:
                if (command.Quantity <= 0)
                    throw new ArgumentOutOfRangeException(nameof(command.Quantity));
                if (command.Quantity > inventory.AvailableQuantity)
                    throw new InvalidOperationException("Insufficient available inventory.");
                break;

            case InventoryTransactionType.Release:
                if (command.Quantity <= 0)
                    throw new ArgumentOutOfRangeException(nameof(command.Quantity));
                break;

            case InventoryTransactionType.Sale:
                if (command.Quantity <= 0)
                    throw new ArgumentOutOfRangeException(nameof(command.Quantity));
                if (command.Quantity > inventory.ReservedQuantity || command.Quantity > inventory.QuantityOnHand)
                    throw new InvalidOperationException("Sale exceeds reserved inventory.");
                break;

            case InventoryTransactionType.ManualAdjustment:
                if (command.AbsoluteQuantityOnHand is null or < 0)
                    throw new ArgumentOutOfRangeException(nameof(command.AbsoluteQuantityOnHand));
                break;
        }
    }
}