namespace CoffeeShopApi.Events.Integration;

// Message published to RabbitMQ when an admin updates an order's status.
// Consumers react to this to send shipping notifications, trigger fulfillment, etc.
public record OrderStatusChangedIntegrationEvent(
    Guid OrderId,
    string UserId,
    string OldStatus,
    string NewStatus,
    DateTime ChangedAt
);
