using CoffeeShopApi.Models;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Data;

public static class DatabaseSeeder
{
    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<CoffeeShopDbContext>();

        // Check if data already exists to avoid duplicate seeding
        if (!await context.Origins.AnyAsync())
        {
            // 1. Seed 10 Origins
            var origins = new List<Origin>
            {
                new Origin { Country = "Ethiopia", Region = "Yirgacheffe" },
                new Origin { Country = "Colombia", Region = "Huila" },
                new Origin { Country = "Brazil", Region = "Minas Gerais" },
                new Origin { Country = "Guatemala", Region = "Antigua" },
                new Origin { Country = "Costa Rica", Region = "Tarrazu" },
                new Origin { Country = "Kenya", Region = "Nyeri" },
                new Origin { Country = "Panama", Region = "Boquete" },
                new Origin { Country = "Indonesia", Region = "Sumatra" },
                new Origin { Country = "Jamaica", Region = "Blue Mountain" },
                new Origin { Country = "Yemen", Region = "Mocha" }
            };
            context.Origins.AddRange(origins);
            await context.SaveChangesAsync(); // Save to get the generated IDs for the beans

            // 2. Seed 10 Coffee Beans
            // Note: Using picsum.photos for reliable placeholder images that won't 404
            var beans = new List<CoffeeBean>
            {
                new CoffeeBean { Name = "Ethiopian Yirgacheffe", SKU = "BEAN-ETH-001", Price = 18.99m, StockQuantity = 50, OriginId = origins[0].Id, RoastLevel = "Light", TastingNotes = "Floral, Citrus, Bergamot", ImageUrl = "https://picsum.photos/seed/bean1/400/400" },
                new CoffeeBean { Name = "Colombia Supremo", SKU = "BEAN-COL-001", Price = 16.50m, StockQuantity = 100, OriginId = origins[1].Id, RoastLevel = "Medium", TastingNotes = "Caramel, Chocolate, Nutty", ImageUrl = "https://picsum.photos/seed/bean2/400/400" },
                new CoffeeBean { Name = "Brazil Cerrado", SKU = "BEAN-BRA-001", Price = 14.00m, StockQuantity = 120, OriginId = origins[2].Id, RoastLevel = "Dark", TastingNotes = "Dark Chocolate, Roasted Almonds", ImageUrl = "https://picsum.photos/seed/bean3/400/400" },
                new CoffeeBean { Name = "Guatemala Antigua", SKU = "BEAN-GUA-001", Price = 17.50m, StockQuantity = 60, OriginId = origins[3].Id, RoastLevel = "Medium", TastingNotes = "Cocoa, Spices, Apple", ImageUrl = "https://picsum.photos/seed/bean4/400/400" },
                new CoffeeBean { Name = "Costa Rica Tarrazu", SKU = "BEAN-COS-001", Price = 19.00m, StockQuantity = 45, OriginId = origins[4].Id, RoastLevel = "Light", TastingNotes = "Honey, Sweet Lime, Milk Chocolate", ImageUrl = "https://picsum.photos/seed/bean5/400/400" },
                new CoffeeBean { Name = "Kenya AA Washed", SKU = "BEAN-KEN-001", Price = 21.00m, StockQuantity = 30, OriginId = origins[5].Id, RoastLevel = "Light", TastingNotes = "Blackberry, Winey, Grapefruit", ImageUrl = "https://picsum.photos/seed/bean6/400/400" },
                new CoffeeBean { Name = "Panama Geisha", SKU = "BEAN-PAN-001", Price = 45.00m, StockQuantity = 15, OriginId = origins[6].Id, RoastLevel = "Light", TastingNotes = "Jasmine, Peach, Earl Grey", ImageUrl = "https://picsum.photos/seed/bean7/400/400" },
                new CoffeeBean { Name = "Sumatra Mandheling", SKU = "BEAN-INA-001", Price = 15.50m, StockQuantity = 80, OriginId = origins[7].Id, RoastLevel = "Dark", TastingNotes = "Earthy, Tobacco, Dark Cocoa", ImageUrl = "https://picsum.photos/seed/bean8/400/400" },
                new CoffeeBean { Name = "Jamaica Blue Mountain", SKU = "BEAN-JAM-001", Price = 55.00m, StockQuantity = 10, OriginId = origins[8].Id, RoastLevel = "Medium", TastingNotes = "Mild, Creamy, Floral", ImageUrl = "https://picsum.photos/seed/bean9/400/400" },
                new CoffeeBean { Name = "Yemen Mocha", SKU = "BEAN-YEM-001", Price = 35.00m, StockQuantity = 20, OriginId = origins[9].Id, RoastLevel = "Medium", TastingNotes = "Dried Fruit, Wine, Chocolate", ImageUrl = "https://picsum.photos/seed/bean10/400/400" }
            };
            context.CoffeeBeans.AddRange(beans);

            // 3. Seed 10 Equipments
            var equipments = new List<Equipment>
            {
                new Equipment { Name = "Hario V60 Ceramic", SKU = "EQ-V60-001", Price = 25.00m, StockQuantity = 20, Brand = "Hario", EquipmentType = "Filter", ImageUrl = "https://picsum.photos/seed/eq1/400/400" },
                new Equipment { Name = "Fellow Stagg EKG", SKU = "EQ-STK-001", Price = 165.00m, StockQuantity = 10, Brand = "Fellow", EquipmentType = "Kettle", ImageUrl = "https://picsum.photos/seed/eq2/400/400" },
                new Equipment { Name = "Comandante C40", SKU = "EQ-CMD-001", Price = 250.00m, StockQuantity = 5, Brand = "Comandante", EquipmentType = "Grinder", ImageUrl = "https://picsum.photos/seed/eq3/400/400" },
                new Equipment { Name = "Chemex Classic 6-Cup", SKU = "EQ-CHX-001", Price = 47.00m, StockQuantity = 15, Brand = "Chemex", EquipmentType = "Filter", ImageUrl = "https://picsum.photos/seed/eq4/400/400" },
                new Equipment { Name = "AeroPress", SKU = "EQ-AER-001", Price = 39.95m, StockQuantity = 30, Brand = "AeroPress", EquipmentType = "Brewer", ImageUrl = "https://picsum.photos/seed/eq5/400/400" },
                new Equipment { Name = "Baratza Encore", SKU = "EQ-BRZ-001", Price = 149.00m, StockQuantity = 8, Brand = "Baratza", EquipmentType = "Grinder", ImageUrl = "https://picsum.photos/seed/eq6/400/400" },
                new Equipment { Name = "Acaia Pearl Scale", SKU = "EQ-ACA-001", Price = 150.00m, StockQuantity = 12, Brand = "Acaia", EquipmentType = "Scale", ImageUrl = "https://picsum.photos/seed/eq7/400/400" },
                new Equipment { Name = "Kalita Wave 185", SKU = "EQ-KAL-001", Price = 28.00m, StockQuantity = 25, Brand = "Kalita", EquipmentType = "Filter", ImageUrl = "https://picsum.photos/seed/eq8/400/400" },
                new Equipment { Name = "Bambino Plus Espresso", SKU = "EQ-BRE-001", Price = 499.00m, StockQuantity = 3, Brand = "Breville", EquipmentType = "Machine", ImageUrl = "https://picsum.photos/seed/eq9/400/400" },
                new Equipment { Name = "WDT Distribution Tool", SKU = "EQ-WDT-001", Price = 15.00m, StockQuantity = 50, Brand = "Generic", EquipmentType = "Accessory", ImageUrl = "https://picsum.photos/seed/eq10/400/400" }
            };
            context.Equipments.AddRange(equipments);

            await context.SaveChangesAsync();
        }
    }
}
