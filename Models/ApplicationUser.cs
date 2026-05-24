using Microsoft.AspNetCore.Identity;

namespace CoffeeShopApi.Models;

public class ApplicationUser : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Billing / shipping address (pre-fill for checkout)
    public string? Phone { get; set; }
    public string? BillingFirstName { get; set; }
    public string? BillingLastName { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }

    // Navigation property
    public Membership? Membership { get; set; }
}
