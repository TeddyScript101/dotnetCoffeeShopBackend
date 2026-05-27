using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.DTOs;

public record RegisterRequest(
    [Required][EmailAddress][MaxLength(256)] string Email,
    [Required][MinLength(6)][MaxLength(100)] string Password,
    [Required][MaxLength(100)] string FirstName,
    [Required][MaxLength(100)] string LastName
);

public record LoginRequest(
    [Required][EmailAddress] string Email,
    [Required] string Password
);

public record AssignRoleRequest(
    [Required][EmailAddress] string Email,
    [Required] string Role
);
