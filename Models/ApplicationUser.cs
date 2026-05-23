using Microsoft.AspNetCore.Identity;

namespace CoffeeShopApi.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Membership? Membership { get; set; }
}
