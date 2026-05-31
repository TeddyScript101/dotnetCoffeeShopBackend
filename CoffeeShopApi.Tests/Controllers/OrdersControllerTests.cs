using CoffeeShopApi.DTOs;
using CoffeeShopApi.Tests.Helpers;
using CoffeeShopApi.Tests.Infrastructure;
using CoffeeShopApi.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CoffeeShopApi.Tests.Controllers;

public class OrdersControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _customerToken = string.Empty;

    public OrdersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.SeedUsersAsync(scope);
        _customerToken = TestJwtTokenFactory.GenerateToken(
            TestDataSeeder.CustomerUserId, TestDataSeeder.CustomerEmail);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- POST /api/orders ----

    [Fact]
    public async Task CreateOrder_WithValidData_Returns201WithCorrectDto()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 10);

        var response = await _client.PostAsync("/api/orders",
            BuildValidOrder(productId).ToJsonContent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal("Processing", order.Status);
        Assert.Equal("Paid", order.PaymentStatus);
        Assert.Equal("4242", order.CardLastFour); // last 4 from FakeStripePaymentService
        Assert.Single(order.Items);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    [Fact]
    public async Task CreateOrder_DeductsStockFromProduct()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 5);

        await _client.PostAsync("/api/orders", BuildValidOrder(productId, quantity: 2).ToJsonContent());

        // Verify stock was reduced
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        var product = await db.Products.FindAsync(productId);
        Assert.NotNull(product);
        Assert.Equal(3, product.StockQuantity); // 5 - 2 = 3
    }

    [Fact]
    public async Task CreateOrder_SubtotalBelow100_ChargesShipping10()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 10);

        var response = await _client.PostAsync("/api/orders",
            BuildValidOrder(productId, quantity: 1).ToJsonContent()); // subtotal = $20

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(20.00m, order.Subtotal);
        Assert.Equal(10.00m, order.ShippingCost);
        Assert.Equal(30.00m, order.Total);
    }

    [Fact]
    public async Task CreateOrder_SubtotalAtExactly100_FreeShipping()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 50.00m, stock: 10);

        var response = await _client.PostAsync("/api/orders",
            BuildValidOrder(productId, quantity: 2).ToJsonContent()); // subtotal = $100

        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(100.00m, order.Subtotal);
        Assert.Equal(0.00m, order.ShippingCost);
        Assert.Equal(100.00m, order.Total);
    }

    [Fact]
    public async Task CreateOrder_WithEmptyCart_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var request = new CreateOrderRequest
        {
            Items = [],
            ShippingAddress = ValidShippingAddress(),
            Payment = ValidPayment(),
        };

        var response = await _client.PostAsync("/api/orders", request.ToJsonContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cart is empty.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task CreateOrder_WithUnconfirmedPaymentIntent_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 10);

        var request = BuildValidOrder(productId);
        request.Payment.PaymentIntentId = "pi_test_unknown_or_not_succeeded";

        var response = await _client.PostAsync("/api/orders", request.ToJsonContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Payment has not been confirmed.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task CreateOrder_WithMissingShippingCity_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 10);

        var request = BuildValidOrder(productId);
        request.ShippingAddress.City = ""; // required field is blank

        var response = await _client.PostAsync("/api/orders", request.ToJsonContent());
        // 400 may come from model binding ([Required]) or the controller — just verify rejection
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithNonExistentProduct_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var request = BuildValidOrder(Guid.NewGuid()); // product does not exist

        var response = await _client.PostAsync("/api/orders", request.ToJsonContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not found", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateOrder_WithInsufficientStock_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 1);

        var request = BuildValidOrder(productId, quantity: 5); // only 1 in stock

        var response = await _client.PostAsync("/api/orders", request.ToJsonContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Insufficient stock", body.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task CreateOrder_WithZeroQuantity_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var productId = await SeedFreshProductAsync(price: 20.00m, stock: 10);

        var request = BuildValidOrder(productId, quantity: 0);

        var response = await _client.PostAsync("/api/orders", request.ToJsonContent());
        // 400 may come from model binding ([Range]) or the controller — just verify rejection
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateOrder_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.PostAsync("/api/orders",
            BuildValidOrder(Guid.NewGuid()).ToJsonContent());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- GET /api/orders ----

    [Fact]
    public async Task GetMyOrders_ReturnsOnlyCurrentUserOrders()
    {
        // Seed an order for a second user and verify it does not appear in customer's list
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        const string otherUserId = "other-user-id-000000000000000000";
        await TestDataSeeder.SeedOrderAsync(db, otherUserId);
        var myOrder = await TestDataSeeder.SeedOrderAsync(db, TestDataSeeder.CustomerUserId);

        _client.SetBearerToken(_customerToken);
        var response = await _client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orders = result.GetProperty("data")
            .Deserialize<List<OrderDto>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(orders);

        // The other user's order must not appear in this customer's list
        Assert.DoesNotContain(orders, o =>
            db.Orders.AsEnumerable().Any(dbOrder => dbOrder.Id == o.Id && dbOrder.UserId == otherUserId));

        // The customer's own order must appear
        Assert.Contains(orders, o => o.Id == myOrder.Id);
    }

    [Fact]
    public async Task GetMyOrders_ReturnsSortedByCreatedAtDescending()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();

        // Seed three orders with staggered CreatedAt times
        static CoffeeShopApi.Models.Order MakeOrder(string userId, DateTime createdAt) =>
            new CoffeeShopApi.Models.Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Status = CoffeeShopApi.Models.OrderStatus.Processing,
                PaymentStatus = CoffeeShopApi.Models.PaymentStatus.Paid,
                ShippingFirstName = "A", ShippingLastName = "B",
                ShippingAddress = "1 St", ShippingCity = "City",
                ShippingState = "ST", ShippingPostalCode = "0000",
                ShippingCountry = "AU", CardLastFour = "1234",
                Subtotal = 10, ShippingCost = 10, Total = 20,
                CreatedAt = createdAt,
                Items = [],
            };

        var o1 = MakeOrder(TestDataSeeder.CustomerUserId, DateTime.UtcNow.AddMinutes(-30));
        var o2 = MakeOrder(TestDataSeeder.CustomerUserId, DateTime.UtcNow.AddMinutes(-20));
        var o3 = MakeOrder(TestDataSeeder.CustomerUserId, DateTime.UtcNow.AddMinutes(-10));
        db.Orders.AddRange(o1, o2, o3);
        await db.SaveChangesAsync();

        _client.SetBearerToken(_customerToken);
        var response = await _client.GetAsync("/api/orders");
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var orders = result.GetProperty("data")
            .Deserialize<List<OrderDto>>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(orders);

        var myOrders = orders.Where(o =>
            new[] { o1.Id, o2.Id, o3.Id }.Contains(o.Id)).ToList();

        Assert.Equal(3, myOrders.Count);
        Assert.True(myOrders[0].CreatedAt >= myOrders[1].CreatedAt);
        Assert.True(myOrders[1].CreatedAt >= myOrders[2].CreatedAt);
    }

    [Fact]
    public async Task GetMyOrders_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- GET /api/orders/{id} ----

    [Fact]
    public async Task GetOrder_ByOwner_Returns200WithFullDetails()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        var order = await TestDataSeeder.SeedOrderAsync(db, TestDataSeeder.CustomerUserId);

        _client.SetBearerToken(_customerToken);
        var response = await _client.GetAsync($"/api/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(dto);
        Assert.Equal(order.Id, dto.Id);
        Assert.NotNull(dto.ShippingAddress);
        Assert.False(string.IsNullOrEmpty(dto.CardLastFour));
    }

    [Fact]
    public async Task GetOrder_ByDifferentUser_Returns404()
    {
        const string otherUserId = "other-user-id-111111111111111111";
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        var order = await TestDataSeeder.SeedOrderAsync(db, otherUserId);

        _client.SetBearerToken(_customerToken); // customer trying to see someone else's order
        var response = await _client.GetAsync($"/api/orders/{order.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WithUnknownId_Returns404()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetOrder_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Helpers ----

    private static CreateOrderRequest BuildValidOrder(Guid productId, int quantity = 1) =>
        new CreateOrderRequest
        {
            Items = [new CreateOrderItemRequest { ProductId = productId, Quantity = quantity }],
            ShippingAddress = ValidShippingAddress(),
            Payment = ValidPayment(),
        };

    private static ShippingAddressRequest ValidShippingAddress() =>
        new ShippingAddressRequest
        {
            FirstName = "John",
            LastName = "Doe",
            Address = "123 Test Street",
            City = "Melbourne",
            State = "VIC",
            PostalCode = "3000",
            Country = "AU",
        };

    private static PaymentRequest ValidPayment() =>
        new PaymentRequest
        {
            PaymentIntentId = FakeStripePaymentService.ValidPaymentIntentId,
        };

    // Seeds a fresh CoffeeBean with the given price and stock, returns its ID.
    private async Task<Guid> SeedFreshProductAsync(decimal price, int stock)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var origin = new CoffeeShopApi.Models.Origin
        {
            Id = Guid.NewGuid(),
            Country = "Colombia",
            Region = "Huila",
        };
        var bean = new CoffeeShopApi.Models.CoffeeBean
        {
            Id = Guid.NewGuid(),
            Name = $"Fresh Bean {suffix}",
            SKU = $"FRESH-{suffix}",
            Price = price,
            StockQuantity = stock,
            OriginId = origin.Id,
            RoastLevel = "Medium",
            TastingNotes = "Sweet",
        };
        db.Origins.Add(origin);
        db.CoffeeBeans.Add(bean);
        await db.SaveChangesAsync();
        return bean.Id;
    }
}
