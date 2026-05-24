using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Data;

public class CoffeeShopDbContext : IdentityDbContext<ApplicationUser>
{
    public CoffeeShopDbContext(DbContextOptions<CoffeeShopDbContext> options) : base(options)
    {
    }

    public DbSet<Membership> Memberships { get; set; }
    public DbSet<Product> Products { get; set; }
    public DbSet<CoffeeBean> CoffeeBeans { get; set; }
    public DbSet<Equipment> Equipments { get; set; }
    public DbSet<Origin> Origins { get; set; }
    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Configure TPT (Table-Per-Type) inheritance for Products
        builder.Entity<CoffeeBean>().ToTable("CoffeeBeans");
        builder.Entity<Equipment>().ToTable("Equipments");

        // Relationships
        builder.Entity<ApplicationUser>()
            .HasOne(u => u.Membership)
            .WithOne(m => m.User)
            .HasForeignKey<Membership>(m => m.UserId);

        builder.Entity<CoffeeBean>()
            .HasOne(cb => cb.Origin)
            .WithMany(o => o.CoffeeBeans)
            .HasForeignKey(cb => cb.OriginId);

        // Order relationships
        builder.Entity<Order>()
            .HasOne(o => o.User)
            .WithMany()
            .HasForeignKey(o => o.UserId);

        builder.Entity<OrderItem>()
            .HasOne(i => i.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(i => i.OrderId);

        // LineTotal is computed — ignore it in EF
        builder.Entity<OrderItem>()
            .Ignore(i => i.LineTotal);

        // Store enums as strings
        builder.Entity<Order>()
            .Property(o => o.Status)
            .HasConversion<string>();

        builder.Entity<Order>()
            .Property(o => o.PaymentStatus)
            .HasConversion<string>();
    }
}
