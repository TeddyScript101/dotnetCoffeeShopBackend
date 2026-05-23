namespace CoffeeShopApi.Models;

public class Membership
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int Points { get; set; } = 0;
    public string Tier { get; set; } = "Bronze";
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ApplicationUser? User { get; set; }
}
