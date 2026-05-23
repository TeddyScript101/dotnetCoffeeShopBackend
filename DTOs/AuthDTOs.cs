namespace CoffeeShopApi.DTOs;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public record LoginRequest(string Email, string Password);
public record AssignRoleRequest(string Email, string Role);
