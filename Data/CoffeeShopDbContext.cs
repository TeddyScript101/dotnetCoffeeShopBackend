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
    }
}
