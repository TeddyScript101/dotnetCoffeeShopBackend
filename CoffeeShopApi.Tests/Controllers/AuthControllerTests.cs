using CoffeeShopApi.Tests.Helpers;
using CoffeeShopApi.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace CoffeeShopApi.Tests.Controllers;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await TestDataSeeder.SeedUsersAsync(scope);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // ---- POST /api/auth/register ----

    [Fact]
    public async Task Register_WithValidData_Returns200AndCreatesUserAndMembership()
    {
        var email = UniqueEmail("reg");
        var response = await _client.PostAsync("/api/auth/register", new
        {
            Email = email,
            Password = "ValidPass123!",
            FirstName = "Alice",
            LastName = "Test",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("message", out _), "Response should have a message field");

        // Verify user and membership exist in the database
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<
                CoffeeShopApi.Models.ApplicationUser>>();
        var db = scope.ServiceProvider
            .GetRequiredService<CoffeeShopApi.Data.CoffeeShopDbContext>();

        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);

        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains("Customer", roles);

        var membership = db.Memberships.FirstOrDefault(m => m.UserId == user.Id);
        Assert.NotNull(membership);
        Assert.Equal(0, membership.Points);
        Assert.Equal("Bronze", membership.Tier);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        // First registration succeeds
        var email = UniqueEmail("dup");
        var payload = new
        {
            Email = email,
            Password = "ValidPass123!",
            FirstName = "Bob",
            LastName = "Test",
        };
        await _client.PostAsync("/api/auth/register", payload.ToJsonContent());

        // Second registration with same email fails
        var response = await _client.PostAsync("/api/auth/register", payload.ToJsonContent());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithWeakPassword_Returns400()
    {
        var response = await _client.PostAsync("/api/auth/register", new
        {
            Email = UniqueEmail("weak"),
            Password = "a",
            FirstName = "C",
            LastName = "Test",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- POST /api/auth/login ----

    [Fact]
    public async Task Login_WithValidCredentials_Returns200WithJwtToken()
    {
        var response = await _client.PostAsync("/api/auth/login", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Password = TestDataSeeder.CustomerPassword,
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("token", out var tokenProp));

        var jwt = tokenProp.GetString();
        Assert.NotNull(jwt);
        Assert.NotEmpty(jwt);
    }

    [Fact]
    public async Task Login_TokenContainsExpectedClaims()
    {
        var response = await _client.PostAsync("/api/auth/login", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Password = TestDataSeeder.CustomerPassword,
        }.ToJsonContent());

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = body.GetProperty("token").GetString()!;

        var handler = new JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(jwt);

        Assert.Equal("CoffeeShopApi", token.Issuer);
        Assert.Contains(token.Claims, c => c.Type == "sub" && !string.IsNullOrEmpty(c.Value));
        Assert.Contains(token.Claims, c => c.Type == "email" && c.Value == TestDataSeeder.CustomerEmail);
        Assert.True(token.ValidTo > DateTime.UtcNow.AddMinutes(90),
            "Token should be valid for close to 2 hours");
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsync("/api/auth/login", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Password = "WrongPassword!",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401()
    {
        var response = await _client.PostAsync("/api/auth/login", new
        {
            Email = "nobody@nowhere.com",
            Password = "SomePassword1!",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- POST /api/auth/assign-role ----

    [Fact]
    public async Task AssignRole_AsAdmin_AssignsRoleAndReturns200()
    {
        // Register a fresh user to receive the role
        var targetEmail = UniqueEmail("target");
        await _client.PostAsync("/api/auth/register", new
        {
            Email = targetEmail,
            Password = "ValidPass123!",
            FirstName = "Target",
            LastName = "User",
        }.ToJsonContent());

        var adminToken = TestJwtTokenFactory.GenerateAdminToken(
            TestDataSeeder.AdminUserId, TestDataSeeder.AdminEmail);
        _client.SetBearerToken(adminToken);

        var response = await _client.PostAsync("/api/auth/assign-role", new
        {
            Email = targetEmail,
            Role = "Admin",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify the role was actually assigned
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<
                CoffeeShopApi.Models.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(targetEmail);
        Assert.NotNull(user);
        var roles = await userManager.GetRolesAsync(user);
        Assert.Contains("Admin", roles);
    }

    [Fact]
    public async Task AssignRole_AsCustomer_Returns403()
    {
        var customerToken = TestJwtTokenFactory.GenerateToken(
            TestDataSeeder.CustomerUserId, TestDataSeeder.CustomerEmail);
        _client.SetBearerToken(customerToken);

        var response = await _client.PostAsync("/api/auth/assign-role", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Role = "Admin",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.PostAsync("/api/auth/assign-role", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Role = "Admin",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_WithUnknownEmail_Returns404()
    {
        var adminToken = TestJwtTokenFactory.GenerateAdminToken(
            TestDataSeeder.AdminUserId, TestDataSeeder.AdminEmail);
        _client.SetBearerToken(adminToken);

        var response = await _client.PostAsync("/api/auth/assign-role", new
        {
            Email = "doesnotexist@nowhere.com",
            Role = "Customer",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AssignRole_WithInvalidRole_Returns400()
    {
        var adminToken = TestJwtTokenFactory.GenerateAdminToken(
            TestDataSeeder.AdminUserId, TestDataSeeder.AdminEmail);
        _client.SetBearerToken(adminToken);

        var response = await _client.PostAsync("/api/auth/assign-role", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Role = "SuperAdmin",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("Invalid role", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);
    }

    // ---- POST /api/auth/logout ----

    [Fact]
    public async Task Logout_WithValidToken_Returns204()
    {
        var token = TestJwtTokenFactory.GenerateToken(
            TestDataSeeder.CustomerUserId, TestDataSeeder.CustomerEmail);
        _client.SetBearerToken(token);

        var response = await _client.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokedToken_Returns401OnSubsequentRequest()
    {
        // Login via the real endpoint to get a token with a jti claim
        var loginResponse = await _client.PostAsync("/api/auth/login", new
        {
            Email = TestDataSeeder.CustomerEmail,
            Password = TestDataSeeder.CustomerPassword,
        }.ToJsonContent());
        var body = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var jwt = body.GetProperty("token").GetString()!;

        _client.SetBearerToken(jwt);

        // Logout — token jti should be added to the blacklist
        await _client.PostAsync("/api/auth/logout", null);

        // Using the same token should now return 401
        var response = await _client.GetAsync("/api/account/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- Helpers ----

    private static string UniqueEmail(string prefix) =>
        $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}@test.com";
}
