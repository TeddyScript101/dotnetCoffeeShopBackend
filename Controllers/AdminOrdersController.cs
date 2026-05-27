using CoffeeShopApi.Data;
using CoffeeShopApi.Events.Integration;
using CoffeeShopApi.Models;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/admin/orders")]
[Authorize(Roles = "Admin")]
public class AdminOrdersController : ControllerBase
{
    private readonly CoffeeShopDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;

    public AdminOrdersController(CoffeeShopDbContext db, IPublishEndpoint publishEndpoint)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
    }

    // GET /api/admin/orders — list all orders (admin view), paginated
    [HttpGet]
    public async Task<IActionResult> GetAllOrders(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync();
        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                CustomerEmail = _db.Users.Where(u => u.Id == o.UserId).Select(u => u.Email).FirstOrDefault(),
                o.Status,
                o.PaymentStatus,
                o.Total,
                o.CreatedAt,
                ItemCount = o.Items.Count,
            })
            .ToListAsync();

        return Ok(new
        {
            data = orders,
            page,
            pageSize,
            total,
            totalPages = (int)Math.Ceiling((double)total / pageSize),
        });
    }

    // PATCH /api/admin/orders/{id}/status — update order status and publish event
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusRequest req)
    {
        if (!Enum.TryParse<OrderStatus>(req.Status, ignoreCase: true, out var newStatus))
            return BadRequest(new { message = $"Invalid status '{req.Status}'. Valid values: Processing, Shipped, Delivered, Cancelled." });

        var order = await _db.Orders.FindAsync(id);
        if (order is null)
            return NotFound(new { message = $"Order {id} not found." });

        // Validate the status transition
        var validationError = ValidateTransition(order.Status, newStatus);
        if (validationError is not null)
            return BadRequest(new { message = validationError });

        var oldStatus = order.Status;
        order.Status = newStatus;
        await _db.SaveChangesAsync();

        // Publish event to RabbitMQ — consumers handle notifications, fulfillment, etc.
        await _publishEndpoint.Publish(new OrderStatusChangedIntegrationEvent(
            OrderId: order.Id,
            UserId: order.UserId,
            OldStatus: oldStatus.ToString(),
            NewStatus: newStatus.ToString(),
            ChangedAt: DateTime.UtcNow
        ));

        return Ok(new
        {
            message = $"Order {id} updated from {oldStatus} to {newStatus}.",
            orderId = order.Id,
            oldStatus = oldStatus.ToString(),
            newStatus = newStatus.ToString(),
        });
    }

    // Enforces valid status transitions — returns an error message or null if valid.
    private static string? ValidateTransition(OrderStatus current, OrderStatus next)
    {
        if (current == next)
            return $"Order is already in '{next}' status.";

        if (current == OrderStatus.Delivered)
            return "Cannot change status of a delivered order.";

        if (current == OrderStatus.Cancelled)
            return "Cannot change status of a cancelled order.";

        var allowed = current switch
        {
            OrderStatus.Processing => new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
            OrderStatus.Shipped => new[] { OrderStatus.Delivered, OrderStatus.Cancelled },
            _ => Array.Empty<OrderStatus>(),
        };

        if (!allowed.Contains(next))
            return $"Cannot transition from '{current}' to '{next}'. Allowed: {string.Join(", ", allowed)}.";

        return null;
    }
}

public record UpdateOrderStatusRequest(string Status);
