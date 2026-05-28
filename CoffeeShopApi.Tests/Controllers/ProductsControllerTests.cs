using CoffeeShopApi.DTOs;
using CoffeeShopApi.Tests.Helpers;
using CoffeeShopApi.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CoffeeShopApi.Tests.Controllers;

public class ProductsControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    // Seeded once per class; test methods reference these IDs.
    private Guid _beanId;
    private Guid _equipmentId;
    private Guid _originId;
    private string _adminToken = string.Empty;
    private string _customerToken = string.Empty;

    public ProductsControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.SeedUsersAsync(scope);

        // Seed products once — idempotency is handled by unique SKUs per seeder call.
        // Re-seeding on every test init would add extra rows, which is harmless for
        // the read tests (they see all products) but we track the IDs seeded on the
        // first call via an outer check.
        var db = scope.ServiceProvider.GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();
        if (!db.Products.Any())
        {
            var (bean, equipment, origin) = await TestDataSeeder.SeedProductsAsync(db);
            _beanId = bean.Id;
            _equipmentId = equipment.Id;
            _originId = origin.Id;
        }
        else
        {
            _beanId = db.CoffeeBeans.Select(b => b.Id).First();
            _equipmentId = db.Equipments.Select(e => e.Id).First();
            _originId = db.Origins.Select(o => o.Id).First();
        }

        _adminToken = TestJwtTokenFactory.GenerateAdminToken(
            TestDataSeeder.AdminUserId, TestDataSeeder.AdminEmail);
        _customerToken = TestJwtTokenFactory.GenerateToken(
            TestDataSeeder.CustomerUserId, TestDataSeeder.CustomerEmail);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- GET /api/products ----

    [Fact]
    public async Task GetAll_ReturnsAllProducts_WithoutAuth()
    {
        _client.ClearBearerToken();
        var response = await _client.GetAsync("/api/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<ProductDto>>(
            HttpClientExtensions.ApiJsonOptions);
        Assert.NotNull(products);
        Assert.NotEmpty(products);
    }

    [Fact]
    public async Task GetAll_ReturnsCorrectProductTypes()
    {
        var response = await _client.GetAsync("/api/products");
        var products = await response.Content.ReadFromJsonAsync<List<JsonElement>>();
        Assert.NotNull(products);

        // Products list should contain at least one CoffeeBean and one Equipment
        Assert.Contains(products, p =>
            p.GetProperty("productType").GetString() == "CoffeeBean");
        Assert.Contains(products, p =>
            p.GetProperty("productType").GetString() == "Equipment");
    }

    // ---- GET /api/products/coffeebeans ----

    [Fact]
    public async Task GetCoffeeBeans_ReturnsBeansWithOriginFields()
    {
        var response = await _client.GetAsync("/api/products/coffeebeans");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // API serializes ProductType enum as a string — use ApiJsonOptions to deserialize.
        var beans = await response.Content.ReadFromJsonAsync<List<CoffeeBeanDto>>(
            HttpClientExtensions.ApiJsonOptions);
        Assert.NotNull(beans);
        Assert.NotEmpty(beans);

        var bean = beans.First();
        Assert.False(string.IsNullOrEmpty(bean.OriginCountry));
        Assert.False(string.IsNullOrEmpty(bean.OriginRegion));
        Assert.False(string.IsNullOrEmpty(bean.RoastLevel));
        Assert.False(string.IsNullOrEmpty(bean.TastingNotes));
    }

    // ---- GET /api/products/equipments ----

    [Fact]
    public async Task GetEquipments_ReturnsEquipmentList()
    {
        var response = await _client.GetAsync("/api/products/equipments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var equipments = await response.Content.ReadFromJsonAsync<List<EquipmentDto>>(
            HttpClientExtensions.ApiJsonOptions);
        Assert.NotNull(equipments);
        Assert.NotEmpty(equipments);

        var equipment = equipments.First();
        Assert.False(string.IsNullOrEmpty(equipment.Brand));
        Assert.False(string.IsNullOrEmpty(equipment.EquipmentType));
    }

    // ---- GET /api/products/{id} ----

    [Fact]
    public async Task GetById_CoffeeBean_ReturnsCoffeeBeanDtoShape()
    {
        var response = await _client.GetAsync($"/api/products/{_beanId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_beanId.ToString(), body.GetProperty("id").GetString());
        Assert.True(body.TryGetProperty("roastLevel", out _),
            "CoffeeBeanDto should include roastLevel");
        Assert.True(body.TryGetProperty("originCountry", out _),
            "CoffeeBeanDto should include originCountry");
    }

    [Fact]
    public async Task GetById_Equipment_ReturnsEquipmentDtoShape()
    {
        var response = await _client.GetAsync($"/api/products/{_equipmentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(_equipmentId.ToString(), body.GetProperty("id").GetString());
        Assert.True(body.TryGetProperty("brand", out _), "EquipmentDto should include brand");
        Assert.True(body.TryGetProperty("equipmentType", out _),
            "EquipmentDto should include equipmentType");
    }

    [Fact]
    public async Task GetById_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync($"/api/products/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- POST /api/products/coffeebeans ----

    [Fact]
    public async Task CreateCoffeeBean_AsAdmin_Returns201WithId()
    {
        _client.SetBearerToken(_adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsync("/api/products/coffeebeans", new CreateCoffeeBeanRequest
        {
            Name = $"New Bean {suffix}",
            SKU = $"NEW-BEAN-{suffix}",
            Price = 35.00m,
            StockQuantity = 20,
            RoastLevel = "Medium",
            TastingNotes = "Chocolate, Nutty",
            OriginId = _originId,
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        // The API serializes with camelCase, so the property is "id" not "Id".
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("id", out var idProp));
        Assert.True(Guid.TryParse(idProp.GetString(), out _));
    }

    [Fact]
    public async Task CreateCoffeeBean_WithNonExistentOriginId_Returns400()
    {
        _client.SetBearerToken(_adminToken);

        var response = await _client.PostAsync("/api/products/coffeebeans", new CreateCoffeeBeanRequest
        {
            Name = "Ghost Bean",
            SKU = "GHOST-001",
            Price = 10.00m,
            StockQuantity = 5,
            RoastLevel = "Dark",
            TastingNotes = "Smoky",
            OriginId = Guid.NewGuid(), // does not exist
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateCoffeeBean_AsCustomer_Returns403()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.PostAsync("/api/products/coffeebeans", new CreateCoffeeBeanRequest
        {
            Name = "Sneaky Bean",
            SKU = "SNEAKY-001",
            Price = 10.00m,
            StockQuantity = 1,
            RoastLevel = "Light",
            TastingNotes = "Fruity",
            OriginId = _originId,
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateCoffeeBean_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.PostAsync("/api/products/coffeebeans",
            new CreateCoffeeBeanRequest
            {
                Name = "Anon Bean",
                SKU = "ANON-001",
                Price = 5m,
                StockQuantity = 1,
                RoastLevel = "Light",
                TastingNotes = "Plain",
                OriginId = _originId,
            }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- PUT /api/products/coffeebeans/{id} ----

    [Fact]
    public async Task UpdateCoffeeBean_AsAdmin_Returns204AndPersistsChanges()
    {
        // Create a fresh bean to update so we don't affect other tests
        _client.SetBearerToken(_adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var createResp = await _client.PostAsync("/api/products/coffeebeans",
            new CreateCoffeeBeanRequest
            {
                Name = $"Update Target Bean {suffix}",
                SKU = $"UPD-{suffix}",
                Price = 25.00m,
                StockQuantity = 5,
                RoastLevel = "Light",
                TastingNotes = "Floral",
                OriginId = _originId,
            }.ToJsonContent());
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var newId = Guid.Parse(createBody.GetProperty("id").GetString()!);

        // Update the bean
        var putResponse = await _client.PutAsync($"/api/products/coffeebeans/{newId}",
            new UpdateCoffeeBeanRequest
            {
                Name = $"Updated Bean {suffix}",
                SKU = $"UPD-{suffix}",
                Price = 30.00m,
                StockQuantity = 10,
                RoastLevel = "Dark",
                TastingNotes = "Smoky, Rich",
                OriginId = _originId,
            }.ToJsonContent());

        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        // Confirm the change is visible via GET
        var getResp = await _client.GetAsync($"/api/products/{newId}");
        var getBody = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Dark", getBody.GetProperty("roastLevel").GetString());
        Assert.Equal(30.00m, getBody.GetProperty("price").GetDecimal());
    }

    [Fact]
    public async Task UpdateCoffeeBean_WithUnknownId_Returns404()
    {
        _client.SetBearerToken(_adminToken);
        var response = await _client.PutAsync($"/api/products/coffeebeans/{Guid.NewGuid()}",
            new UpdateCoffeeBeanRequest
            {
                Name = "Ghost",
                SKU = "GHOST",
                Price = 1m,
                StockQuantity = 1,
                RoastLevel = "Light",
                TastingNotes = "None",
                OriginId = _originId,
            }.ToJsonContent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCoffeeBean_AsCustomer_Returns403()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.PutAsync($"/api/products/coffeebeans/{_beanId}",
            new UpdateCoffeeBeanRequest
            {
                Name = "Hack",
                SKU = "HACK",
                Price = 1m,
                StockQuantity = 1,
                RoastLevel = "Light",
                TastingNotes = "None",
                OriginId = _originId,
            }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- POST /api/products/equipments ----

    [Fact]
    public async Task CreateEquipment_AsAdmin_Returns201()
    {
        _client.SetBearerToken(_adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsync("/api/products/equipments", new CreateEquipmentRequest
        {
            Name = $"New Grinder {suffix}",
            SKU = $"GRD-{suffix}",
            Price = 150.00m,
            StockQuantity = 8,
            Brand = "AcmeCo",
            EquipmentType = "Grinder",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
    }

    [Fact]
    public async Task CreateEquipment_AsCustomer_Returns403()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.PostAsync("/api/products/equipments", new CreateEquipmentRequest
        {
            Name = "Sneaky Grinder",
            SKU = "SNEAKY-GRD",
            Price = 50m,
            StockQuantity = 1,
            Brand = "BadActor",
            EquipmentType = "Grinder",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- PUT /api/products/equipments/{id} ----

    [Fact]
    public async Task UpdateEquipment_AsAdmin_Returns204()
    {
        // Create a fresh equipment to update
        _client.SetBearerToken(_adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var createResp = await _client.PostAsync("/api/products/equipments",
            new CreateEquipmentRequest
            {
                Name = $"Update Target Eq {suffix}",
                SKU = $"UPD-EQ-{suffix}",
                Price = 60m,
                StockQuantity = 3,
                Brand = "OldBrand",
                EquipmentType = "Scale",
            }.ToJsonContent());
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var newId = Guid.Parse(createBody.GetProperty("id").GetString()!);

        var putResponse = await _client.PutAsync($"/api/products/equipments/{newId}",
            new UpdateEquipmentRequest
            {
                Name = $"Updated Eq {suffix}",
                SKU = $"UPD-EQ-{suffix}",
                Price = 75m,
                StockQuantity = 5,
                Brand = "NewBrand",
                EquipmentType = "Scale",
            }.ToJsonContent());

        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateEquipment_WithUnknownId_Returns404()
    {
        _client.SetBearerToken(_adminToken);
        var response = await _client.PutAsync($"/api/products/equipments/{Guid.NewGuid()}",
            new UpdateEquipmentRequest
            {
                Name = "Ghost",
                SKU = "GHOST",
                Price = 1m,
                StockQuantity = 1,
                Brand = "Ghost",
                EquipmentType = "Grinder",
            }.ToJsonContent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- DELETE /api/products/{id} ----

    [Fact]
    public async Task DeleteProduct_AsAdmin_Returns204AndSubsequentGetReturns404()
    {
        // Create a fresh product so we don't destroy the shared seeded product
        _client.SetBearerToken(_adminToken);
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var createResp = await _client.PostAsync("/api/products/equipments",
            new CreateEquipmentRequest
            {
                Name = $"Disposable {suffix}",
                SKU = $"DEL-{suffix}",
                Price = 10m,
                StockQuantity = 1,
                Brand = "DeleteMe",
                EquipmentType = "Accessory",
            }.ToJsonContent());
        // The API serializes with camelCase, so the property is "id" not "Id".
        var createBody = await createResp.Content.ReadFromJsonAsync<JsonElement>();
        var deleteId = Guid.Parse(createBody.GetProperty("id").GetString()!);

        var deleteResponse = await _client.DeleteAsync($"/api/products/{deleteId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/products/{deleteId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_AsCustomer_Returns403()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.DeleteAsync($"/api/products/{_equipmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProduct_WithNonExistentId_Returns404()
    {
        _client.SetBearerToken(_adminToken);
        var response = await _client.DeleteAsync($"/api/products/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
