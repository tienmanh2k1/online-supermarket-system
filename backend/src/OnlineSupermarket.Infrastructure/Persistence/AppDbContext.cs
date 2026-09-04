using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Branches;
using OnlineSupermarket.Domain.Catalog;
using OnlineSupermarket.Domain.Identity;
using OnlineSupermarket.Domain.Inventory;
using OnlineSupermarket.Domain.Orders;
using OnlineSupermarket.Domain.Entities;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Domain.Promotions;
using OnlineSupermarket.Domain.Recommendations;
using OnlineSupermarket.Domain.Reviews;
using OnlineSupermarket.Domain.Shopping;
using OnlineSupermarket.Domain.Jobs;

namespace OnlineSupermarket.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    // Identity
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    // Catalog
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();

    // Inventory
    public DbSet<BranchInventory> BranchInventories => Set<BranchInventory>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();

    // Shopping
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Address> Addresses => Set<Address>();

    // Orders
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();

    // Payments
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentCallback> PaymentCallbacks => Set<PaymentCallback>();

    // Promotions
    public DbSet<Promotion> Promotions => Set<Promotion>();

    // Reviews
    public DbSet<Review> Reviews => Set<Review>();

    // Recommendations
    public DbSet<ProductViewEvent> ProductViewEvents => Set<ProductViewEvent>();
    public DbSet<RecommendationResult> RecommendationResults => Set<RecommendationResult>();

    // Jobs
    public DbSet<BackgroundJobRun> BackgroundJobRuns => Set<BackgroundJobRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<OrderStatusHistory>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.State = EntityState.Unchanged;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
