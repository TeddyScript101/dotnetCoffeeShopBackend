using CoffeeShopApi.Data;
using CoffeeShopApi.DTOs;
using CoffeeShopApi.Services;
using Microsoft.AspNetCore.RateLimiting;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.OrdinalIgnoreCase) { "Admin", "Customer" };

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly CoffeeShopDbContext _db;
    private readonly ITokenBlacklist _blacklist;

    public AuthController(UserManager<ApplicationUser> userManager, IConfiguration configuration, CoffeeShopDbContext db, ITokenBlacklist blacklist)
    {
        _userManager = userManager;
        _configuration = configuration;
        _db = db;
        _blacklist = blacklist;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        var user = new ApplicationUser { UserName = req.Email, Email = req.Email, FirstName = req.FirstName, LastName = req.LastName };
        var result = await _userManager.CreateAsync(user, req.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Customer");

            // Create a Membership record so the user can earn points from day one
            _db.Memberships.Add(new Membership
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Points = 0,
                Tier = "Bronze",
                JoinedAt = DateTime.UtcNow,
            });
            await _db.SaveChangesAsync();

            return Ok(new { Message = "User registered successfully" });
        }
        return BadRequest(result.Errors);
    }

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, req.Password))
        {
            return Unauthorized();
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key (Jwt:Key) is not configured."));
        
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var userRoles = await _userManager.GetRolesAsync(user);
        foreach (var role in userRoles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddHours(2),
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        var jwt = tokenHandler.WriteToken(token);

        return Ok(new { Token = jwt });
    }

    [Authorize]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        var jti = User.FindFirstValue("jti");
        if (jti is not null)
            _blacklist.Revoke(jti, DateTime.UtcNow.AddHours(2));
        return NoContent();
    }

    // DELETE /api/auth/account — permanently delete the authenticated user's account.
    // Restores product stock for any orders before removing the user;
    // DB cascade handles Orders, OrderItems, and Membership rows.
    // Intended for e2e test teardown only.
    [Authorize]
    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        // Read orders + items BEFORE deletion so we know how much stock to restore
        var orders = await _db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId)
            .ToListAsync();

        await using var transaction = await _db.Database.BeginTransactionAsync();

        foreach (var order in orders)
        {
            foreach (var item in order.Items)
            {
                await _db.Products
                    .Where(p => p.Id == item.ProductId)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(p => p.StockQuantity, p => p.StockQuantity + item.Quantity));
            }
        }

        // UserManager.DeleteAsync + DB cascade removes the user and all related rows
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync();
            return StatusCode(500, new { errors = result.Errors.Select(e => e.Description) });
        }

        await transaction.CommitAsync();
        return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("assign-role")]
    public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest req)
    {
        if (!AllowedRoles.Contains(req.Role))
            return BadRequest(new { message = $"Invalid role. Allowed: {string.Join(", ", AllowedRoles)}." });

        var user = await _userManager.FindByEmailAsync(req.Email);
        if (user is null) return NotFound("User not found");

        var result = await _userManager.AddToRoleAsync(user, req.Role);
        return result.Succeeded ? Ok(new { Message = $"Role {req.Role} assigned to user" }) : BadRequest(result.Errors);
    }
}
