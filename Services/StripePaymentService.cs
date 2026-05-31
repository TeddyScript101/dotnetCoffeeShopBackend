using Stripe;

namespace CoffeeShopApi.Services;

public class StripePaymentService : IStripePaymentService
{
    public async Task<string> CreatePaymentIntentAsync(long amountInCents, string currency)
    {
        var service = new PaymentIntentService();
        var pi = await service.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = amountInCents,
            Currency = currency,
            PaymentMethodTypes = ["card"],
        });
        return pi.ClientSecret;
    }

    public async Task<PaymentVerificationResult> VerifyPaymentIntentAsync(string paymentIntentId)
    {
        var service = new PaymentIntentService();
        var options = new PaymentIntentGetOptions();
        options.AddExpand("payment_method");
        PaymentIntent pi;
        try
        {
            pi = await service.GetAsync(paymentIntentId, options);
        }
        catch (StripeException)
        {
            return new PaymentVerificationResult(false, "****");
        }
        var last4 = pi.PaymentMethod?.Card?.Last4 ?? "****";
        return new PaymentVerificationResult(pi.Status == "succeeded", last4);
    }
}
