using CoffeeShopApi.Data;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShopApi.Tests.Infrastructure;

/// <summary>
/// Idempotent helpers for seeding deterministic test data into the test database.
/// All seeding methods are safe to call multiple times within the same test class
/// (they check before inserting so the shared database is not corrupted between tests).
/// </summary>
public static class TestDataSeeder
{
    // Fixed IDs / credentials shared across all tests in a test session.
    public const string CustomerUserId = "customer-test-000000000000000000";
    public const string CustomerEmail = "customer@coffeetest.com";
    public const string CustomerPassword = "Customer123!";

    public const string AdminUserId = "admin-test-0000000000000000000000";
    public const string AdminEmail = "admin@coffeetest.com";
    public const string AdminPassword = "Admin123!";

    /// <summary>
    /// Seeds a Customer and an Admin user. Safe to call multiple times — skips
    /// creation if the user already exists.
    /// </summary>
    public static async Task SeedUsersAsync(IServiceScope scope)
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();

        if (await userManager.FindByEmailAsync(CustomerEmail) is null)
        {
            var customer = new ApplicationUser
            {
                Id = CustomerUserId,
                UserName = CustomerEmail,
                Email = CustomerEmail,
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "Customer",
            };
            await userManager.CreateAsync(customer, CustomerPassword);
            await userManager.AddToRoleAsync(customer, "Customer");

            db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                UserId = CustomerUserId,
                Points = 0,
                Tier = "Bronze",
                JoinedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        if (await userManager.FindByEmailAsync(AdminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                Id = AdminUserId,
                UserName = AdminEmail,
                Email = AdminEmail,
                EmailConfirmed = true,
                FirstName = "Test",
                LastName = "Admin",
            };
            await userManager.CreateAsync(admin, AdminPassword);
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }

    /// <summary>
    /// Seeds one Origin, one CoffeeBean, and one Equipment with unique SKUs.
    /// Returns the created entities so tests can reference their IDs.
    /// </summary>
    public static async Task<(CoffeeBean Bean, Equipment Equipment, Origin Origin)> SeedProductsAsync(
        CoffeeShopDbContext db)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var origin = new Origin
        {
            Id = Guid.NewGuid(),
            Country = "Ethiopia",
            Region = "Yirgacheffe",
        };

        var bean = new CoffeeBean
        {
            Id = Guid.NewGuid(),
            Name = $"Test Bean {suffix}",
            SKU = $"BEAN-{suffix}",
            Price = 20.00m,
            StockQuantity = 50,
            OriginId = origin.Id,
            RoastLevel = "Light",
            TastingNotes = "Floral, Citrus",
        };

        var equipment = new Equipment
        {
            Id = Guid.NewGuid(),
            Name = $"Test Grinder {suffix}",
            SKU = $"EQ-{suffix}",
            Price = 80.00m,
            StockQuantity = 10,
            Brand = "TestBrand",
            EquipmentType = "Grinder",
        };

        db.Origins.Add(origin);
        db.CoffeeBeans.Add(bean);
        db.Equipments.Add(equipment);
        await db.SaveChangesAsync();

        return (bean, equipment, origin);
    }

    /// <summary>
    /// Seeds an Order in the given status belonging to the given userId.
    /// If no user with that ID exists, a minimal stub user is inserted first so
    /// SQLite's FK constraint on Orders.UserId is satisfied.
    /// The productId is optional — a random GUID is used if omitted (enough for status tests).
    /// </summary>
    public static async Task<Order> SeedOrderAsync(
        CoffeeShopDbContext db,
        string userId,
        OrderStatus status = OrderStatus.Processing,
        Guid? productId = null)
    {
        // Satisfy the FK Order.UserId -> AspNetUsers.Id without going through UserManager.
        // Use the full userId as the email local-part so different user IDs never collide.
        if (!db.Users.Any(u => u.Id == userId))
        {
            var email = $"{userId}@test.invalid";
            db.Users.Add(new ApplicationUser
            {
                Id = userId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                FirstName = "Stub",
                LastName = "User",
            });
            await db.SaveChangesAsync();
        }

        var orderId = Guid.NewGuid();

        var order = new Order
        {
            Id = orderId,
            UserId = userId,
            Status = status,
            PaymentStatus = PaymentStatus.Paid,
            ShippingFirstName = "Test",
            ShippingLastName = "User",
            ShippingAddress = "1 Test Street",
            ShippingCity = "Testville",
            ShippingState = "TS",
            ShippingPostalCode = "12345",
            ShippingCountry = "AU",
            CardLastFour = "4242",
            Subtotal = 20.00m,
            ShippingCost = 10.00m,
            Total = 30.00m,
            CreatedAt = DateTime.UtcNow,
            Items =
            [
                new OrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ProductId = productId ?? Guid.NewGuid(),
                    ProductName = "Test Product",
                    ProductSku = $"SKU-{Guid.NewGuid().ToString("N")[..8]}",
                    ProductType = "CoffeeBean",
                    UnitPrice = 20.00m,
                    Quantity = 1,
                }
            ],
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }
}
