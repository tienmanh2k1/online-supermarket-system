using Microsoft.EntityFrameworkCore;
using OnlineSupermarket.Domain.Payments;
using OnlineSupermarket.Infrastructure.Persistence;

namespace OnlineSupermarket.Api.Tests.Persistence;

public sealed class PaymentConfigurationTests
{
    [Fact]
    public void Payment_IsMock_IsRequiredAndDefaultsToFalse()
    {
        using var context = CreateContext();

        var property = context.Model.FindEntityType(typeof(Payment))!.FindProperty("IsMock");

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
        Assert.Equal(false, property.GetDefaultValue());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
