using CoffeeShopApi.Data;
using CoffeeShopApi.Events.Consumers;
using CoffeeShopApi.Events.Integration;
using CoffeeShopApi.Models;
using MassTransit;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoffeeShopApi.Tests.Consumers;

/// <summary>
/// Tests for OrderCreatedConsumer in isolation — no HTTP pipeline.
/// Uses the InMemory EF Core provider so no FK constraints are enforced,
/// allowing Membership records to be seeded without creating full Identity users.
/// </summary>
public class OrderCreatedConsumerTests : IAsyncLifetime
{
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    // Explicit root ensures ALL DbContext instances within this test instance share
    // the same in-memory store, bypassing EF Core's global InternalServiceProvider cache.
    private readonly InMemoryDatabaseRoot _dbRoot = new();
    private readonly string _dbName = $"ConsumerTest_{Guid.NewGuid():N}";

    private const string TestUserId = "consumer-test-user-id";

    // Seed these user IDs before the harness starts so MassTransit's consumer scope
    // sees the data immediately. Seeding after Start() can race with the consumer.
    private const string ZeroPointsUserId = "consumer-test-zero-points";
    private const string AccumulateUserId = "consumer-accumulate-id";
    private const string PersistUserId = "consumer-persist-id";

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddLogging();

        // Explicit InMemoryDatabaseRoot guarantees that every DbContext created from
        // this service provider shares the exact same in-memory store, regardless of
        // EF Core's global InternalServiceProvider cache.
        services.AddDbContext<CoffeeShopDbContext>(opt =>
            opt.UseInMemoryDatabase(_dbName, _dbRoot));

        // Test harness registers the consumer and an in-memory MassTransit bus.
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderCreatedConsumer>();
        });

        _provider = services.BuildServiceProvider();

        // Seed all memberships BEFORE starting the harness so the consumer's scope
        // always sees them (avoids any race between seeding and message delivery).
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        db.Memberships.AddRange(
            new Membership { Id = Guid.NewGuid(), UserId = TestUserId,       Points = 0,   Tier = "Bronze", JoinedAt = DateTime.UtcNow },
            new Membership { Id = Guid.NewGuid(), UserId = ZeroPointsUserId, Points = 10,  Tier = "Bronze", JoinedAt = DateTime.UtcNow },
            new Membership { Id = Guid.NewGuid(), UserId = AccumulateUserId, Points = 100, Tier = "Silver", JoinedAt = DateTime.UtcNow },
            new Membership { Id = Guid.NewGuid(), UserId = PersistUserId,    Points = 0,   Tier = "Bronze", JoinedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    // Waits for at least `count` messages to be fully processed by OrderCreatedConsumer.
    // Uses Select<T>() (synchronous snapshot) in a polling loop since Any<T> only
    // guarantees the FIRST message was seen; a count-aware overload does not exist.
    private async Task WaitForConsumerCountAsync(int count, int timeoutMs = 10_000)
    {
        var consumerHarness = _harness.GetConsumerHarness<OrderCreatedConsumer>();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var received = consumerHarness.Consumed
                .Select<OrderCreatedIntegrationEvent>()
                .ToList();
            if (received.Count >= count)
                return;
            await Task.Delay(50);
        }
        throw new TimeoutException(
            $"Timed out waiting for {count} consumed messages (saw {consumerHarness.Consumed.Select<OrderCreatedIntegrationEvent>().Count()}).");
    }

    [Fact]
    public async Task Consume_AwardsFloorOfTotalAsPoints()
    {
        var consumerHarness = _harness.GetConsumerHarness<OrderCreatedConsumer>();

        await _harness.Bus.Publish(new OrderCreatedIntegrationEvent(
            OrderId: Guid.NewGuid(),
            UserId: TestUserId,
            Total: 45.75m,
            ItemCount: 2,
            CreatedAt: DateTime.UtcNow));

        // Any<T>() waits until the consumer's Consume method returns — SaveChangesAsync is done.
        Assert.True(await consumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>(),
            "Consumer should have processed the event");

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var membership = await db.Memberships.FirstAsync(m => m.UserId == TestUserId);
        Assert.Equal(45, membership.Points); // Math.Floor(45.75) = 45
    }

    [Fact]
    public async Task Consume_AwardsZeroPoints_WhenTotalLessThanOne()
    {
        // ZeroPointsUserId seeded in InitializeAsync with Points = 10
        var consumerHarness = _harness.GetConsumerHarness<OrderCreatedConsumer>();

        await _harness.Bus.Publish(new OrderCreatedIntegrationEvent(
            OrderId: Guid.NewGuid(),
            UserId: ZeroPointsUserId,
            Total: 0.50m, // Math.Floor(0.50) = 0 points awarded
            ItemCount: 1,
            CreatedAt: DateTime.UtcNow));

        Assert.True(await consumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>());

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var membership = await db.Memberships.FirstAsync(m => m.UserId == ZeroPointsUserId);
        Assert.Equal(10, membership.Points); // unchanged — zero points awarded
    }

    [Fact]
    public async Task Consume_AccumulatesPointsAcrossMultipleOrders()
    {
        // AccumulateUserId seeded in InitializeAsync with Points = 100
        await _harness.Bus.Publish(new OrderCreatedIntegrationEvent(
            Guid.NewGuid(), AccumulateUserId, Total: 30.0m, ItemCount: 1, DateTime.UtcNow));

        await _harness.Bus.Publish(new OrderCreatedIntegrationEvent(
            Guid.NewGuid(), AccumulateUserId, Total: 25.0m, ItemCount: 1, DateTime.UtcNow));

        // Poll until both messages are fully processed by the consumer
        await WaitForConsumerCountAsync(count: 2);

        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var membership = await db.Memberships.FirstAsync(m => m.UserId == AccumulateUserId);
        Assert.Equal(155, membership.Points); // 100 + 30 + 25
    }

    [Fact]
    public async Task Consume_WhenNoMembershipFound_LogsWarningAndDoesNotThrow()
    {
        const string unknownUserId = "user-with-no-membership";
        var consumerHarness = _harness.GetConsumerHarness<OrderCreatedConsumer>();

        await _harness.Bus.Publish(new OrderCreatedIntegrationEvent(
            Guid.NewGuid(), unknownUserId, Total: 50m, ItemCount: 1, DateTime.UtcNow));

        // Consumer should complete without faulting the message
        Assert.True(await consumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>());
        Assert.False(await consumerHarness.Consumed.Any<Fault<OrderCreatedIntegrationEvent>>());
    }

    [Fact]
    public async Task Consume_PersistsPointsToDatabase()
    {
        // PersistUserId seeded in InitializeAsync with Points = 0
        var consumerHarness = _harness.GetConsumerHarness<OrderCreatedConsumer>();

        await _harness.Bus.Publish(new OrderCreatedIntegrationEvent(
            Guid.NewGuid(), PersistUserId, Total: 99.99m, ItemCount: 3, DateTime.UtcNow));

        // consumerHarness.Consumed.Any<T>() returns only after the consumer's Consume
        // method has returned — SaveChangesAsync is guaranteed to be done.
        Assert.True(await consumerHarness.Consumed.Any<OrderCreatedIntegrationEvent>(),
            "Consumer should have processed the event");

        // Open a new scope to verify the write is visible across context instances
        using var scope = _provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopDbContext>();
        var membership = await db.Memberships.AsNoTracking()
            .FirstAsync(m => m.UserId == PersistUserId);
        Assert.Equal(99, membership.Points); // Math.Floor(99.99) = 99
    }
}
