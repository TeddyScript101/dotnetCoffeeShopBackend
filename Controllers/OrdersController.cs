using CoffeeShopApi.Data;
using CoffeeShopApi.DTOs;
using CoffeeShopApi.Events.Integration;
using CoffeeShopApi.Models;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly CoffeeShopDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;

    public OrdersController(CoffeeShopDbContext db, IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    // POST /api/orders — place a new order
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (req.Items == null || req.Items.Count == 0)
            return BadRequest(new { message = "Cart is empty." });

        // Validate shipping address
        var addr = req.ShippingAddress;
        if (string.IsNullOrWhiteSpace(addr.FirstName) ||
            string.IsNullOrWhiteSpace(addr.LastName) ||
            string.IsNullOrWhiteSpace(addr.Address) ||
            string.IsNullOrWhiteSpace(addr.City) ||
            string.IsNullOrWhiteSpace(addr.PostalCode) ||
            string.IsNullOrWhiteSpace(addr.Country))
        {
            return BadRequest(new { message = "Incomplete shipping address." });
        }

        // Validate simulated card (just check length)
        var cardNumber = req.Payment?.CardNumber?.Replace(" ", "") ?? "";
        if (cardNumber.Length < 13 || cardNumber.Length > 19 || !cardNumber.All(char.IsDigit))
            return BadRequest(new { message = "Invalid card number." });

        // Fetch products from DB to get canonical prices
        var productIds = req.Items.Select(i => i.ProductId).ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync();

        var orderItems = new List<OrderItem>();

        foreach (var reqItem in req.Items)
        {
            var product = products.FirstOrDefault(p => p.Id == reqItem.ProductId);
            if (product is null)
                return BadRequest(new { message = $"Product {reqItem.ProductId} not found." });

            if (reqItem.Quantity <= 0)
                return BadRequest(new { message = "Quantity must be at least 1." });

            if (product.StockQuantity < reqItem.Quantity)
                return BadRequest(new { message = $"Insufficient stock for {product.Name}." });

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.SKU,
                ProductImageUrl = product.ImageUrl,
                ProductType = product.GetType().Name,
                UnitPrice = product.Price,
                Quantity = reqItem.Quantity,
            });
        }

        // Deduct stock
        foreach (var reqItem in req.Items)
        {
            var product = products.First(p => p.Id == reqItem.ProductId);
            product.StockQuantity -= reqItem.Quantity;
        }

        var subtotal = orderItems.Sum(i => i.UnitPrice * i.Quantity);
        var shippingCost = subtotal >= 100m ? 0m : 10m; // Free shipping over $100
        var total = subtotal + shippingCost;

        var order = new Order
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Items = orderItems,
            ShippingFirstName = addr.FirstName,
            ShippingLastName = addr.LastName,
            ShippingAddress = addr.Address,
            ShippingCity = addr.City,
            ShippingState = addr.State ?? string.Empty,
            ShippingPostalCode = addr.PostalCode,
            ShippingCountry = addr.Country,
            CardLastFour = cardNumber[^4..],
            PaymentStatus = PaymentStatus.Paid, // Simulated — always succeeds
            Status = OrderStatus.Processing,
            Subtotal = subtotal,
            ShippingCost = shippingCost,
            Total = total,
            CreatedAt = DateTime.UtcNow,
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync();

        // Publish event to RabbitMQ — consumers handle points, notifications, etc.
        await _publishEndpoint.Publish(new OrderCreatedIntegrationEvent(
            OrderId: order.Id,
            UserId: userId,
            Total: order.Total,
            ItemCount: orderItems.Count,
            CreatedAt: order.CreatedAt
        ));

        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, MapToDto(order));
    }

    // GET /api/orders — list my orders (most recent first)
    [HttpGet]
    public async Task<IActionResult> GetMyOrders()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var orders = await _db.Orders
            .Where(o => o.UserId == userId)
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(MapToDto));
    }

    // GET /api/orders/{id} — get a single order
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order is null) return NotFound();

        return Ok(MapToDto(order));
    }

    // ---- Helpers ----

    private static OrderDto MapToDto(Order order) => new()
    {
        Id = order.Id,
        Items = order.Items.Select(i => new OrderItemDto
        {
            Id = i.Id,
            ProductId = i.ProductId,
            ProductName = i.ProductName,
            ProductSku = i.ProductSku,
            ProductImageUrl = i.ProductImageUrl,
            ProductType = i.ProductType,
            UnitPrice = i.UnitPrice,
            Quantity = i.Quantity,
            LineTotal = i.UnitPrice * i.Quantity,
        }).ToList(),
        ShippingAddress = new ShippingAddressDto
        {
            FirstName = order.ShippingFirstName,
            LastName = order.ShippingLastName,
            Address = order.ShippingAddress,
            City = order.ShippingCity,
            State = order.ShippingState,
            PostalCode = order.ShippingPostalCode,
            Country = order.ShippingCountry,
        },
        CardLastFour = order.CardLastFour,
        PaymentStatus = order.PaymentStatus.ToString(),
        Status = order.Status.ToString(),
        Subtotal = order.Subtotal,
        ShippingCost = order.ShippingCost,
        Total = order.Total,
        CreatedAt = order.CreatedAt,
    };
}

// Helper alias so we can use the JWT claim name inside the controller
file static class JwtRegisteredClaimNames
{
    public const string Sub = "sub";
}
