using CoffeeShopApi.Events.Integration;
using MassTransit;

namespace CoffeeShopApi.Events.Consumers;

// Listens to the OrderStatusChangedIntegrationEvent queue.
// Logs a notification for each status transition.
// This is a placeholder — swap in a real email/push service later without touching any other code.
public class OrderStatusChangedConsumer : IConsumer<OrderStatusChangedIntegrationEvent>
{
    private readonly ILogger<OrderStatusChangedConsumer> _logger;

    public OrderStatusChangedConsumer(ILogger<OrderStatusChangedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderStatusChangedIntegrationEvent> context)
    {
        var msg = context.Message;

        _logger.LogInformation(
            "[Notification] Order {OrderId} for user {UserId}: {OldStatus} -> {NewStatus} at {ChangedAt}",
            msg.OrderId, msg.UserId, msg.OldStatus, msg.NewStatus, msg.ChangedAt);

        // Future: send email, push notification, trigger fulfillment system, etc.
        await Task.CompletedTask;
    }
}
