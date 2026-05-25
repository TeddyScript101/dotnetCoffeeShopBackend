namespace CoffeeShopApi.Events.Integration;

// Message published to RabbitMQ when a new order is placed.
// Consumers react to this to award membership points, send confirmation emails, etc.
public record OrderCreatedIntegrationEvent(
    Guid OrderId,
    string UserId,
    decimal Total,
    int ItemCount,
    DateTime CreatedAt
);
