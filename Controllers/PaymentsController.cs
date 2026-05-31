using CoffeeShopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace CoffeeShopApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentsController : ControllerBase
{
    private readonly IStripePaymentService _stripe;

    public PaymentsController(IStripePaymentService stripe)
    {
        _stripe = stripe;
    }

    [HttpPost("create-payment-intent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentRequest req)
    {
        var clientSecret = await _stripe.CreatePaymentIntentAsync(req.AmountInCents, "aud");
        return Ok(new { clientSecret });
    }
}

public class CreatePaymentIntentRequest
{
    [Required, Range(1, 10_000_000)]
    public long AmountInCents { get; set; }
}
