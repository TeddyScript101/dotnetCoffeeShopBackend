using CoffeeShopApi.DTOs;
using CoffeeShopApi.Tests.Helpers;
using CoffeeShopApi.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace CoffeeShopApi.Tests.Controllers;

public class AccountControllerTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private string _customerToken = string.Empty;

    public AccountControllerTests(CustomWebApplicationFactory factory)
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

    // ---- GET /api/account/profile ----

    [Fact]
    public async Task GetProfile_WithValidToken_Returns200WithProfileData()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.GetAsync("/api/account/profile");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Equal(TestDataSeeder.CustomerEmail, profile.Email);
        Assert.Equal(0, profile.Points);
        Assert.Equal("Bronze", profile.Tier);
        Assert.NotNull(profile.MemberSince);
    }

    [Fact]
    public async Task GetProfile_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.GetAsync("/api/account/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WithExpiredToken_Returns401()
    {
        var expired = TestJwtTokenFactory.GenerateExpiredToken(
            TestDataSeeder.CustomerUserId, TestDataSeeder.CustomerEmail);
        _client.SetBearerToken(expired);
        var response = await _client.GetAsync("/api/account/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- PUT /api/account/profile ----

    [Fact]
    public async Task UpdateProfile_WithValidData_Returns200AndPersistsChanges()
    {
        _client.SetBearerToken(_customerToken);

        var updateReq = new UpdateProfileRequest
        {
            Phone = "+61 400 000 001",
            BillingFirstName = "Alice",
            BillingLastName = "Smith",
            BillingAddress = "42 Test Ave",
            BillingCity = "Melbourne",
            BillingState = "VIC",
            BillingPostalCode = "3000",
            BillingCountry = "AU",
        };

        var putResponse = await _client.PutAsync("/api/account/profile", updateReq.ToJsonContent());
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        var updated = await putResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(updated);
        Assert.Equal("+61 400 000 001", updated.Phone);
        Assert.Equal("Alice", updated.BillingFirstName);
        Assert.Equal("Melbourne", updated.BillingCity);

        // Confirm persistence via a fresh GET
        var getResponse = await _client.GetAsync("/api/account/profile");
        var fetched = await getResponse.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(fetched);
        Assert.Equal("+61 400 000 001", fetched.Phone);
        Assert.Equal("Melbourne", fetched.BillingCity);
    }

    [Fact]
    public async Task UpdateProfile_WithNullFields_Returns200AndClearsValues()
    {
        _client.SetBearerToken(_customerToken);

        // First set some billing data
        await _client.PutAsync("/api/account/profile", new UpdateProfileRequest
        {
            Phone = "+61 400 000 002",
            BillingCity = "Sydney",
        }.ToJsonContent());

        // Now clear all fields
        var clearReq = new UpdateProfileRequest(); // all nulls
        var response = await _client.PutAsync("/api/account/profile", clearReq.ToJsonContent());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var profile = await response.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.NotNull(profile);
        Assert.Null(profile.Phone);
        Assert.Null(profile.BillingCity);
    }

    [Fact]
    public async Task UpdateProfile_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.PutAsync("/api/account/profile",
            new UpdateProfileRequest { Phone = "0400000000" }.ToJsonContent());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- PUT /api/account/change-password ----

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Returns200()
    {
        // Register a dedicated user for this test so we don't break the shared customer
        var email = $"pwchange-{Guid.NewGuid().ToString("N")[..8]}@test.com";
        await _client.PostAsync("/api/auth/register", new
        {
            Email = email,
            Password = "OldPassword1!",
            FirstName = "Pw",
            LastName = "Test",
        }.ToJsonContent());

        // Log in to get the real token
        var loginResp = await _client.PostAsync("/api/auth/login", new
        {
            Email = email,
            Password = "OldPassword1!",
        }.ToJsonContent());
        var loginBody = await loginResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var token = loginBody.GetProperty("token").GetString()!;
        _client.SetBearerToken(token);

        var response = await _client.PutAsync("/api/account/change-password", new
        {
            CurrentPassword = "OldPassword1!",
            NewPassword = "NewPassword1!",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains("successfully", body.GetProperty("message").GetString()!,
            StringComparison.OrdinalIgnoreCase);

        // Verify the new password works for login
        _client.ClearBearerToken();
        var reloginResp = await _client.PostAsync("/api/auth/login", new
        {
            Email = email,
            Password = "NewPassword1!",
        }.ToJsonContent());
        Assert.Equal(HttpStatusCode.OK, reloginResp.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.PutAsync("/api/account/change-password", new
        {
            CurrentPassword = "ThisIsWrong999!",
            NewPassword = "NewPassword1!",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithNewPasswordTooShort_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.PutAsync("/api/account/change-password", new
        {
            CurrentPassword = TestDataSeeder.CustomerPassword,
            NewPassword = "abc",
        }.ToJsonContent());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Contains("6", body.GetProperty("message").GetString()!);
    }

    [Fact]
    public async Task ChangePassword_WithEmptyCurrentPassword_Returns400()
    {
        _client.SetBearerToken(_customerToken);
        var response = await _client.PutAsync("/api/account/change-password", new
        {
            CurrentPassword = "",
            NewPassword = "NewPassword1!",
        }.ToJsonContent());

        // 400 may come from model binding ([Required]) or the controller — just verify rejection
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        _client.ClearBearerToken();
        var response = await _client.PutAsync("/api/account/change-password", new
        {
            CurrentPassword = "OldPass1!",
            NewPassword = "NewPass1!",
        }.ToJsonContent());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
