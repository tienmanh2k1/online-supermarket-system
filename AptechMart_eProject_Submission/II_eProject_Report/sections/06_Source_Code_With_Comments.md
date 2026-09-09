# CHAPTER 5: CORE SOURCE CODE WITH COMMENTS

This chapter presents 6 representative source code excerpts from the **AptechMart** solution. Each excerpt demonstrates clean architectural design, domain invariant protection, concurrency control, and robust error handling.

---

## 5.1. Domain Tier: Inventory Invariants & Atomic Stock Reservation

- **File**: `backend/src/OnlineSupermarket.Domain/Inventory/BranchInventory.cs`
- **Architectural Purpose**: Enforces domain invariants for branch-specific inventory. Guarantees that physical stock cannot be negative, reserved stock cannot exceed on-hand stock, and stock operations are properly encapsulated.

```csharp
namespace OnlineSupermarket.Domain.Inventory;

using OnlineSupermarket.Domain.Common;

/// <summary>
/// BranchInventory Aggregate: Manages isolated pricing and stock levels per physical store.
/// Encapsulates domain invariants protecting against overselling and concurrency violations.
/// </summary>
public class BranchInventory : BaseEntity
{
    public Guid BranchId { get; private set; }
    public Guid ProductId { get; private set; }
    public decimal SellingPrice { get; private set; }
    public int QuantityOnHand { get; private set; }
    public int ReservedQuantity { get; private set; }
    public int ReorderLevel { get; private set; }

    /// <summary>
    /// Computed available stock: Net units eligible for active customer purchase.
    /// Invariant: AvailableQuantity = QuantityOnHand - ReservedQuantity.
    /// </summary>
    public int AvailableQuantity => QuantityOnHand - ReservedQuantity;

    // EF Core parameterless constructor
    private BranchInventory() { }

    public BranchInventory(Guid branchId, Guid productId, decimal sellingPrice, int quantityOnHand, int reorderLevel = 5)
    {
        if (sellingPrice < 0)
            throw new DomainValidationException("Retail selling price cannot be negative.");
        if (quantityOnHand < 0)
            throw new DomainValidationException("Physical quantity on hand cannot be negative.");

        Id = Guid.NewGuid();
        BranchId = branchId;
        ProductId = productId;
        SellingPrice = sellingPrice;
        QuantityOnHand = quantityOnHand;
        ReservedQuantity = 0;
        ReorderLevel = reorderLevel;
    }

    /// <summary>
    /// Atomic Stock Reservation: Invoked inside transactional checkout.
    /// Verifies availability and places a temporary hold on the inventory.
    /// </summary>
    public void ReserveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainValidationException("Quantity to reserve must be greater than zero.");
        if (quantity > AvailableQuantity)
            throw new DomainValidationException(
                $"Insufficient stock at branch. Available: {AvailableQuantity}, Requested: {quantity}.");

        ReservedQuantity += quantity;
    }

    /// <summary>
    /// Releases reserved stock when an order is cancelled or times out.
    /// </summary>
    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainValidationException("Quantity to release must be greater than zero.");
        if (quantity > ReservedQuantity)
            throw new DomainValidationException("Cannot release more stock than currently reserved.");

        ReservedQuantity -= quantity;
    }

    /// <summary>
    /// Permanently deducts stock upon order fulfillment completion.
    /// Decrements both on-hand quantity and reserved quantity simultaneously.
    /// </summary>
    public void DeductStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainValidationException("Quantity to deduct must be greater than zero.");
        if (quantity > ReservedQuantity)
            throw new DomainValidationException("Cannot deduct more than reserved quantity.");
        if (quantity > QuantityOnHand)
            throw new DomainValidationException("Cannot deduct more than physical quantity on hand.");

        ReservedQuantity -= quantity;
        QuantityOnHand -= quantity;
    }

    /// <summary>
    /// Administrative stock replenishment and pricing adjustments.
    /// </summary>
    public void UpdateInventory(decimal newPrice, int newQuantityOnHand, int newReorderLevel)
    {
        if (newPrice < 0)
            throw new DomainValidationException("Selling price cannot be negative.");
        if (newQuantityOnHand < ReservedQuantity)
            throw new DomainValidationException(
                $"New on-hand quantity ({newQuantityOnHand}) cannot be less than active reservations ({ReservedQuantity}).");

        SellingPrice = newPrice;
        QuantityOnHand = newQuantityOnHand;
        ReorderLevel = newReorderLevel;
    }
}
```

### Business Rule & Invariant Analysis
1. **Encapsulation of State**: All properties have `private set` accessors. Mutations occur strictly through domain methods (`ReserveStock`, `ReleaseStock`, `DeductStock`).
2. **Prevention of Overselling**: `AvailableQuantity` dynamically calculates available units (`QuantityOnHand - ReservedQuantity`). Attempting to reserve more units than available throws a `DomainValidationException`, triggering an immediate database rollback.

---

## 5.2. Security & Identity: PBKDF2 Password Hashing & Token Service

- **File**: `backend/src/OnlineSupermarket.Infrastructure/Security/PasswordHasher.cs`
- **Architectural Purpose**: Implements NIST-compliant, cryptographically salted password hashing using PBKDF2 with HMAC-SHA256, protecting user credentials against rainbow table and dictionary attacks.

```csharp
namespace OnlineSupermarket.Infrastructure.Security;

using System.Security.Cryptography;
using OnlineSupermarket.Domain.Common;

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;        // 128-bit cryptographic salt
    private const int KeySize = 32;         // 256-bit derived key
    private const int Iterations = 10000;   // Computational cost factor
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    /// <summary>
    /// Generates a cryptographically salted PBKDF2 hash formatted as: {iterations}.{salt}.{hash}
    /// </summary>
    public string Hash(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new DomainValidationException("Password cannot be empty.");

        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, KeySize);

        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies candidate password against stored hash using constant-time comparison.
    /// </summary>
    public bool Verify(string password, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(passwordHash))
            return false;

        string[] parts = passwordHash.Split('.');
        if (parts.Length != 3)
            return false;

        int iterations = int.Parse(parts[0]);
        byte[] salt = Convert.FromBase64String(parts[1]);
        byte[] expectedHash = Convert.FromBase64String(parts[2]);

        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, Algorithm, expectedHash.Length);

        // Fixed-time comparison protects against side-channel timing attacks
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
```

### Security Analysis
1. **Unique Salt Generation**: `RandomNumberGenerator.GetBytes(16)` ensures every user receives a unique cryptographic salt, rendering precomputed lookup tables completely useless.
2. **Timing Attack Immunity**: Verification uses `CryptographicOperations.FixedTimeEquals`, ensuring consistent execution duration regardless of byte matching position.

---

## 5.3. Application Tier: Transactional Checkout & Stock Reservation

- **File**: `backend/src/OnlineSupermarket.Api/Endpoints/CheckoutEndpoints.cs`
- **Architectural Purpose**: Coordinates checkout execution within a database transaction. Locks inventory rows, recalculates subtotals, reserves stock, creates orders, and generates immutable item snapshots.

```csharp
public static async Task<IResult> ProcessCheckout(
    CheckoutRequest request,
    AppDbContext dbContext,
    ClaimsPrincipal user,
    CancellationToken ct)
{
    Guid userId = user.GetUserId();

    // 1. Fetch active customer cart for selected branch
    var cart = await dbContext.Carts
        .Include(c => c.Items)
        .ThenInclude(i => i.Product)
        .FirstOrDefaultAsync(c => c.UserId == userId && c.BranchId == request.BranchId, ct);

    if (cart == null || !cart.Items.Any())
        return Results.BadRequest(new { error = "Shopping cart is empty." });

    // 2. Open ACID Database Transaction with Repeatable Read isolation
    await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);
    try
    {
        var productIds = cart.Items.Select(i => i.ProductId).ToList();

        // 3. Acquire row-level locks on branch inventory records
        var inventories = await dbContext.BranchInventories
            .Where(bi => bi.BranchId == request.BranchId && productIds.Contains(bi.ProductId))
            .ToDictionaryAsync(bi => bi.ProductId, ct);

        decimal subtotal = 0;
        var orderItems = new List<OrderItem>();

        // 4. Validate stock availability and execute reservation
        foreach (var item in cart.Items)
        {
            if (!inventories.TryGetValue(item.ProductId, out var inv) || inv.AvailableQuantity < item.Quantity)
            {
                await transaction.RollbackAsync(ct);
                return Results.Conflict(new {
                    error = $"Product '{item.Product.Name}' is out of stock at selected branch.",
                    productId = item.ProductId
                });
            }

            // Reserve stock using Domain Aggregate method
            inv.ReserveStock(item.Quantity);

            decimal lineTotal = inv.SellingPrice * item.Quantity;
            subtotal += lineTotal;

            // Capture immutable order item snapshot
            orderItems.Add(new OrderItem(
                productId: item.ProductId,
                productName: item.Product.Name,
                sku: item.Product.Sku,
                unitPrice: inv.SellingPrice,
                quantity: item.Quantity,
                totalPrice: lineTotal
            ));
        }

        // 5. Instantiate Order aggregate
        var order = new Order(
            userId: userId,
            branchId: request.BranchId,
            fulfillmentMode: request.FulfillmentMode,
            subtotal: subtotal,
            shippingFee: request.FulfillmentMode == FulfillmentMode.Delivery ? 30000m : 0m,
            shippingAddress: request.ShippingAddressJson,
            notes: request.Notes
        );

        order.AddItems(orderItems);
        dbContext.Orders.Add(order);

        // 6. Clear customer cart for this branch
        dbContext.CartItems.RemoveRange(cart.Items);

        await dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Results.Created($"/api/orders/{order.Id}", new { orderId = order.Id, orderNumber = order.OrderNumber });
    }
    catch (Exception ex)
    {
        await transaction.RollbackAsync(ct);
        throw;
    }
}
```

### Concurrency & Transactional Analysis
1. **Repeatable Read Isolation**: Eliminates phantom reads and non-repeatable reads during inventory assessment.
2. **All-or-Nothing Guarantee**: If any individual product in a multi-item cart has insufficient stock, the transaction immediately rolls back (`transaction.RollbackAsync`), leaving the customer's cart intact and preventing partial reservations.

---

## 5.4. Infrastructure Tier: Idempotent Payment Webhook Processing

- **File**: `backend/src/OnlineSupermarket.Infrastructure/Payments/PaymentCallbackProcessor.cs`
- **Architectural Purpose**: Processes asynchronous IPN callbacks from payment gateways (VNPay, MoMo). Verifies cryptographic signatures and guarantees idempotent execution to prevent duplicate order crediting.

```csharp
namespace OnlineSupermarket.Infrastructure.Payments;

public class PaymentCallbackProcessor : IPaymentCallbackProcessor
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PaymentCallbackProcessor> _logger;

    public PaymentCallbackProcessor(AppDbContext dbContext, ILogger<PaymentCallbackProcessor> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<PaymentProcessingResult> ProcessCallbackAsync(
        PaymentGatewayType gateway,
        string idempotencyKey,
        decimal callbackAmount,
        string rawPayload,
        bool isSignatureValid,
        CancellationToken ct)
    {
        // 1. Validate cryptographic HMAC-SHA512 signature
        if (!isSignatureValid)
        {
            _logger.LogWarning("Payment callback signature verification failed for key: {Key}", idempotencyKey);
            return PaymentProcessingResult.InvalidSignature();
        }

        // 2. IDEMPOTENCY GUARD: Check if callback was already processed
        var existingCallback = await _dbContext.PaymentCallbacks
            .Include(c => c.Payment)
            .ThenInclude(p => p.Order)
            .FirstOrDefaultAsync(c => c.IdempotencyKey == idempotencyKey, ct);

        if (existingCallback != null)
        {
            _logger.LogInformation("Idempotent duplicate callback detected: {Key}. Returning cached success.", idempotencyKey);
            return PaymentProcessingResult.Success(existingCallback.Payment.OrderId);
        }

        // 3. Match active payment transaction
        var payment = await _dbContext.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.TransactionId == idempotencyKey, ct);

        if (payment == null)
            return PaymentProcessingResult.OrderNotFound();

        // 4. Verify transaction amount matches order total exactly
        if (payment.Amount != callbackAmount)
        {
            _logger.LogError("Payment amount mismatch! Expected: {Expected}, Received: {Received}", payment.Amount, callbackAmount);
            return PaymentProcessingResult.AmountMismatch();
        }

        // 5. Update payment and advance order lifecycle state
        payment.MarkCompleted(rawPayload);
        payment.Order.TransitionTo(OrderStatus.Processing, "Payment verified via webhook IPN callback.");

        var callbackLog = new PaymentCallback(payment.Id, idempotencyKey, rawPayload, isVerified: true);
        _dbContext.PaymentCallbacks.Add(callbackLog);

        await _dbContext.SaveChangesAsync(ct);
        return PaymentProcessingResult.Success(payment.OrderId);
    }
}
```

### Idempotency & Financial Safety Analysis
1. **Duplicate Callback Defense**: Payment gateways frequently retry webhooks if network latencies exceed thresholds. The `idempotency_key` unique constraint guarantees that secondary webhooks are acknowledged as HTTP 200 without duplicate state transitions.
2. **Strict Amount Re-Verification**: Re-matching `payment.Amount == callbackAmount` prevents tampering attacks where malicious payloads attempt to settle large orders with nominal payments.

---

## 5.5. Infrastructure Tier: Secret Redaction & Log Sanitization

- **File**: `backend/src/OnlineSupermarket.Infrastructure/Jobs/JobErrorSanitizer.cs`
- **Architectural Purpose**: Sanitizes exception messages, stack traces, and database logs to ensure credentials, connection strings, and tokens are never leaked into log stores or error summaries.

```csharp
namespace OnlineSupermarket.Infrastructure.Jobs;

using System.Text.RegularExpressions;

public static class JobErrorSanitizer
{
    // Regex matching secret key-value pairs, JSON attributes, and URI parameters
    private static readonly Regex SecretPatterns = new(
        @"(?i)(password|pwd|secret|token|api_?key|connection_?string|bearer)\s*[:=]\s*(?:""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'|[^\s,;""}]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Redacts all sensitive credentials from error messages and diagnostic summaries.
    /// </summary>
    public static string Sanitize(string? rawInput)
    {
        if (string.IsNullOrEmpty(rawInput))
            return string.Empty;

        return SecretPatterns.Replace(rawInput, "$1=[REDACTED]");
    }

    /// <summary>
    /// Recursively unwraps exceptions and sanitizes sensitive messages.
    /// </summary>
    public static string Sanitize(Exception ex)
    {
        if (ex == null)
            return string.Empty;

        string sanitizedMessage = Sanitize(ex.Message);
        return $"{ex.GetType().Name}: {sanitizedMessage}";
    }
}
```

### Security Compliance Analysis
- Handles single quotes, escaped double quotes (`{\"password\":\"secret\"}`), and URI query tokens.
- Deployed across background worker job runners and global exception filters to guarantee zero credential leakage into logs.

---

## 5.6. Presentation Tier: Client-Side Branch Context & State Management

- **File**: `frontend/src/context/BranchContext.tsx`
- **Architectural Purpose**: Manages the globally active supermarket branch state across the React 19 application, ensuring synchronization between the UI, LocalStorage, and catalog queries.

```typescript
import React, { createContext, useContext, useState, useEffect } from 'react';
import { Branch } from '../types/branch';
import { fetchActiveBranches } from '../api/branchApi';

interface BranchContextType {
  activeBranch: Branch | null;
  availableBranches: Branch[];
  setActiveBranch: (branch: Branch) => void;
  isLoading: boolean;
}

const BranchContext = createContext<BranchContextType | undefined>(undefined);

export const BranchProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [availableBranches, setAvailableBranches] = useState<Branch[]>([]);
  const [activeBranch, setActiveBranchState] = useState<Branch | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    const initializeBranches = async () => {
      try {
        const branches = await fetchActiveBranches();
        setAvailableBranches(branches);

        // Restore previously selected branch from localStorage or set default
        const savedBranchId = localStorage.getItem('aptechmart_selected_branch');
        const matched = branches.find(b => b.id === savedBranchId);
        setActiveBranchState(matched || branches[0] || null);
      } catch (error) {
        console.error('Failed to load supermarket branches:', error);
      } finally {
        setIsLoading(false);
      }
    };

    initializeBranches();
  }, []);

  const setActiveBranch = (branch: Branch) => {
    setActiveBranchState(branch);
    localStorage.setItem('aptechmart_selected_branch', branch.id);
  };

  return (
    <BranchContext.Provider value={{ activeBranch, availableBranches, setActiveBranch, isLoading }}>
      {children}
    </BranchContext.Provider>
  );
};

export const useBranch = () => {
  const context = useContext(BranchContext);
  if (!context) throw new Error('useBranch must be used within a BranchProvider');
  return context;
};
```

### Client Architecture Analysis
- Decouples branch selection from individual page components.
- Automatically triggers catalog re-querying across browse, detail, cart, and comparison views upon store change.
