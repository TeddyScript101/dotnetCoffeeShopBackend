namespace CoffeeShopApi.Models;

public class CoffeeBean : Product
{
    public Guid OriginId { get; set; }
    public string RoastLevel { get; set; } = string.Empty; // Light, Medium, Dark
    public string TastingNotes { get; set; } = string.Empty;

    // Navigation
    public Origin? Origin { get; set; }
}
