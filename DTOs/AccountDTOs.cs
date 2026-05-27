using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.DTOs;

public class UserProfileDto
{
    public string Email { get; set; } = string.Empty;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? BillingFirstName { get; set; }
    public string? BillingLastName { get; set; }
    public string? BillingAddress { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingState { get; set; }
    public string? BillingPostalCode { get; set; }
    public string? BillingCountry { get; set; }

    // Membership / loyalty info
    public int Points { get; set; }
    public string Tier { get; set; } = "Bronze";
    public DateTime? MemberSince { get; set; }
}

public class UpdateProfileRequest
{
    [MaxLength(20)]
    public string? Phone { get; set; }

    [MaxLength(100)]
    public string? BillingFirstName { get; set; }

    [MaxLength(100)]
    public string? BillingLastName { get; set; }

    [MaxLength(200)]
    public string? BillingAddress { get; set; }

    [MaxLength(100)]
    public string? BillingCity { get; set; }

    [MaxLength(100)]
    public string? BillingState { get; set; }

    [MaxLength(20)]
    public string? BillingPostalCode { get; set; }

    [MaxLength(100)]
    public string? BillingCountry { get; set; }
}

public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    public string NewPassword { get; set; } = string.Empty;
}
