using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CoffeeShopApi.Tests.Infrastructure;

/// <summary>
/// Generates JWT tokens signed with the same key and issuer/audience as the
/// test configuration, so they pass validation inside the test server without
/// requiring an actual login HTTP call.
/// </summary>
public static class TestJwtTokenFactory
{
    // Must match appsettings.Tests.json Jwt:Key / Jwt:Issuer / Jwt:Audience.
    private const string Key = "ThisIsAVerySecretKeyForJwtAuthenticationWhichShouldBeLongEnough";
    private const string Issuer = "CoffeeShopApi";
    private const string Audience = "CoffeeShopApiUsers";

    public static string GenerateToken(
        string userId,
        string email,
        string role = "Customer",
        DateTime? expires = null)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.Role, role),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expires ?? DateTime.UtcNow.AddHours(2),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string GenerateAdminToken(string userId, string email) =>
        GenerateToken(userId, email, role: "Admin");

    public static string GenerateExpiredToken(string userId, string email) =>
        GenerateToken(userId, email, expires: DateTime.UtcNow.AddHours(-1));
}
