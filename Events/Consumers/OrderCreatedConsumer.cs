using CoffeeShopApi.Data;
using CoffeeShopApi.Events.Integration;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShopApi.Events.Consumers;

// Listens to the OrderCreatedIntegrationEvent queue.
// Awards 1 membership point per $1 spent (rounded down) when a new order is placed.
public class OrderCreatedConsumer : IConsumer<OrderCreatedIntegrationEvent>
{
    private readonly CoffeeShopDbContext _db;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(CoffeeShopDbContext db, ILogger<OrderCreatedConsumer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedIntegrationEvent> context)
    {
        var msg = context.Message;

        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.UserId == msg.UserId);

        if (membership is null)
        {
            _logger.LogWarning("No membership found for user {UserId}, skipping points award for order {OrderId}",
                msg.UserId, msg.OrderId);
            return;
        }

        var pointsToAward = (int)Math.Floor(msg.Total);
        membership.Points += pointsToAward;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "[Points] Awarded {Points} pts to user {UserId} for order {OrderId} (total: ${Total})",
            pointsToAward, msg.UserId, msg.OrderId, msg.Total);
    }
}
