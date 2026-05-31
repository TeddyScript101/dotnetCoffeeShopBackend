using System.ComponentModel.DataAnnotations;
using CoffeeShopApi.Models;

namespace CoffeeShopApi.DTOs;

// ---- Request DTOs ----

public class CreateOrderRequest
{
    [Required]
    public List<CreateOrderItemRequest> Items { get; set; } = [];

    [Required]
    public ShippingAddressRequest ShippingAddress { get; set; } = new();

    [Required]
    public PaymentRequest Payment { get; set; } = new();
}

public class CreateOrderItemRequest
{
    [Required]
    public Guid ProductId { get; set; }

    [Range(1, 100)]
    public int Quantity { get; set; }
}

public class ShippingAddressRequest
{
    [Required][MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required][MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required][MaxLength(200)]
    public string Address { get; set; } = string.Empty;

    [Required][MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    [Required][MaxLength(20)]
    public string PostalCode { get; set; } = string.Empty;

    [Required][MaxLength(100)]
    public string Country { get; set; } = string.Empty;
}

public class PaymentRequest
{
    [Required]
    public string PaymentIntentId { get; set; } = string.Empty;
}

// ---- Response DTOs ----

public class OrderDto
{
    public Guid Id { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
    public ShippingAddressDto ShippingAddress { get; set; } = new();
    public string CardLastFour { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string? ProductImageUrl { get; set; }
    public string ProductType { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

public class ShippingAddressDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}
