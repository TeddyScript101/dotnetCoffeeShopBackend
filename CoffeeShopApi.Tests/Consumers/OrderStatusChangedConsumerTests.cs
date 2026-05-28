using CoffeeShopApi.Events.Consumers;
using CoffeeShopApi.Events.Integration;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoffeeShopApi.Tests.Consumers;

/// <summary>
/// Tests for OrderStatusChangedConsumer in isolation.
/// The consumer is a logging-only placeholder, so we verify it processes messages
/// without errors rather than asserting database side-effects.
/// </summary>
public class OrderStatusChangedConsumerTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderStatusChangedConsumer>();
        });

        _provider = services.BuildServiceProvider();
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task Consume_ValidEvent_CompletesWithoutException()
    {
        await _harness.Bus.Publish(new OrderStatusChangedIntegrationEvent(
            OrderId: Guid.NewGuid(),
            UserId: "any-user-id",
            OldStatus: "Processing",
            NewStatus: "Shipped",
            ChangedAt: DateTime.UtcNow));

        Assert.True(await _harness.Consumed.Any<OrderStatusChangedIntegrationEvent>(),
            "Consumer should have processed the event");

        // Verify the message was not faulted
        var consumerHarness = _harness.GetConsumerHarness<OrderStatusChangedConsumer>();
        Assert.False(await consumerHarness.Consumed.Any<Fault<OrderStatusChangedIntegrationEvent>>(),
            "Consumer should not fault on a valid event");
    }

    [Fact]
    public async Task Consume_MultipleEvents_AllConsumedSuccessfully()
    {
        var events = new[]
        {
            new OrderStatusChangedIntegrationEvent(Guid.NewGuid(), "user-1", "Processing", "Shipped", DateTime.UtcNow),
            new OrderStatusChangedIntegrationEvent(Guid.NewGuid(), "user-2", "Shipped", "Delivered", DateTime.UtcNow),
            new OrderStatusChangedIntegrationEvent(Guid.NewGuid(), "user-3", "Processing", "Cancelled", DateTime.UtcNow),
        };

        foreach (var evt in events)
            await _harness.Bus.Publish(evt);

        // All three should be consumed
        await Task.Delay(300); // allow background consumers to complete
        var consumed = _harness.Consumed.Select<OrderStatusChangedIntegrationEvent>().ToList();
        Assert.True(consumed.Count >= 3, $"Expected at least 3 consumed messages, got {consumed.Count}");
    }
}
