using CoffeeShopApi.Tests.Helpers;
using CoffeeShopApi.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using CoffeeShopApi.Models;

namespace CoffeeShopApi.Tests.Controllers;

public class AdminOrdersControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _adminToken = string.Empty;
    private string _customerToken = string.Empty;

    public AdminOrdersControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.SeedUsersAsync(scope);
        _adminToken = TestJwtTokenFactory.GenerateAdminToken(
            TestDataSeeder.AdminUserId, TestDataSeeder.AdminEmail);
        _customerToken = TestJwtTokenFactory.GenerateToken(
            TestDataSeeder.CustomerUserId, TestDataSeeder.CustomerEmail);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- GET /api/admin/orders ----

    [Fact]
    public async Task GetAllOrders_AsAdmin_Returns200WithAllOrders()
    {
        // Ensure at least one order exists
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        await TestDataSeeder.SeedOrderAsync(db, TestDataSeeder.CustomerUserId);

        _client.SetBearerToken(_adminToken);
        var response = await _client.GetAsync("/api/admin/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(result.TryGetProperty("data", out var dataArray));
        Assert.True(result.TryGetProperty("total", out _));
        var orders = dataArray.EnumerateArray().ToList();
        Assert.NotEmpty(orders);

        var first = orders.First();
        Assert.True(first.TryGetProperty("id", out _));
        Assert.True(first.TryGetProperty("status", out _));
        Assert.True(first.TryGetProperty("total", out _));
        Assert.True(first.TryGetProperty("itemCount", out _));
    }

    [Fact]
    public async Task GetAllOrders_AsCustomer_Returns403()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.GetAsync("/api/admin/orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAllOrders_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.GetAsync("/api/admin/orders");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- PATCH /api/admin/orders/{id}/status ----

    [Fact]
    public async Task UpdateStatus_Processing_To_Shipped_Returns200()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Shipped");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Processing", body.GetProperty("oldStatus").GetString());
        Assert.Equal("Shipped", body.GetProperty("newStatus").GetString());

        // Verify DB was updated
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        var order = await db.Orders.FindAsync(orderId);
        Assert.Equal(OrderStatus.Shipped, order!.Status);
    }

    [Fact]
    public async Task UpdateStatus_Processing_To_Cancelled_Returns200()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Cancelled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Cancelled", body.GetProperty("newStatus").GetString());
    }

    [Fact]
    public async Task UpdateStatus_Shipped_To_Delivered_Returns200()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Shipped);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Delivered");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_Shipped_To_Cancelled_Returns200()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Shipped);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Cancelled");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_Delivered_ToAnything_Returns400()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Delivered);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Cancelled");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("delivered", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_Cancelled_ToAnything_Returns400()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Cancelled);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Processing");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("cancelled", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_Processing_To_Delivered_Returns400_InvalidJump()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.SetBearerToken(_adminToken);

        // Skipping Shipped — not an allowed transition
        var response = await PatchStatus(orderId, "Delivered");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Cannot transition", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_SameStatus_Returns400()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "Processing");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("already in", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_WithInvalidStatusString_Returns400()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.SetBearerToken(_adminToken);

        var response = await PatchStatus(orderId, "FlyingToMars");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Invalid status", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateStatus_WithNonExistentOrderId_Returns404()
    {
        _client.SetBearerToken(_adminToken);
        var response = await PatchStatus(Guid.NewGuid(), "Shipped");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_AsCustomer_Returns403()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.SetBearerToken(_customerToken);

        var response = await PatchStatus(orderId, "Shipped");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_WithoutToken_Returns401()
    {
        var orderId = await SeedOrderWithStatusAsync(OrderStatus.Processing);
        _client.ClearBearerToken();

        var response = await PatchStatus(orderId, "Shipped");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Helpers ----

    private async Task<Guid> SeedOrderWithStatusAsync(OrderStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        var order = await TestDataSeeder.SeedOrderAsync(db, TestDataSeeder.CustomerUserId, status);
        return order.Id;
    }

    private Task<HttpResponseMessage> PatchStatus(Guid orderId, string status) =>
        _client.PatchAsync(
            $"/api/admin/orders/{orderId}/status",
            new { Status = status }.ToJsonContent());
}
