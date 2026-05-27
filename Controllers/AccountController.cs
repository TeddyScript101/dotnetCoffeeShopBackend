using CoffeeShopApi.Data;
using CoffeeShopApi.DTOs;
using CoffeeShopApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CoffeeShopDbContext _db;

    public AccountController(UserManager<ApplicationUser> userManager, CoffeeShopDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    // GET /api/account/profile
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.UserId == userId);

        return Ok(MapToDto(user, membership));
    }

    // PUT /api/account/profile — update billing address + phone
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        user.Phone = req.Phone?.Trim();
        user.BillingFirstName = req.BillingFirstName?.Trim();
        user.BillingLastName = req.BillingLastName?.Trim();
        user.BillingAddress = req.BillingAddress?.Trim();
        user.BillingCity = req.BillingCity?.Trim();
        user.BillingState = req.BillingState?.Trim();
        user.BillingPostalCode = req.BillingPostalCode?.Trim();
        user.BillingCountry = req.BillingCountry?.Trim();

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        var membership = await _db.Memberships
            .FirstOrDefaultAsync(m => m.UserId == userId);

        return Ok(MapToDto(user, membership));
    }

    // PUT /api/account/change-password
    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new { message = "Both current and new password are required." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, req.CurrentPassword, req.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });

        return Ok(new { message = "Password updated successfully." });
    }

    private static UserProfileDto MapToDto(ApplicationUser user, Membership? membership) => new()
    {
        Email = user.Email ?? string.Empty,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Phone = user.Phone,
        BillingFirstName = user.BillingFirstName,
        BillingLastName = user.BillingLastName,
        BillingAddress = user.BillingAddress,
        BillingCity = user.BillingCity,
        BillingState = user.BillingState,
        BillingPostalCode = user.BillingPostalCode,
        BillingCountry = user.BillingCountry,
        Points = membership?.Points ?? 0,
        Tier = membership?.Tier ?? "Bronze",
        MemberSince = membership?.JoinedAt,
    };
}
