using CoffeeShopApi.Services;

namespace CoffeeShopApi.Tests.Infrastructure;

public class FakeStripePaymentService : IStripePaymentService
{
    public const string ValidPaymentIntentId = "pi_test_valid_succeeded";

    public Task<string> CreatePaymentIntentAsync(long amountInCents, string currency)
        => Task.FromResult("pi_test_secret_xyz");

    public Task<PaymentVerificationResult> VerifyPaymentIntentAsync(string paymentIntentId)
    {
        if (paymentIntentId == ValidPaymentIntentId)
            return Task.FromResult(new PaymentVerificationResult(true, "4242"));

        return Task.FromResult(new PaymentVerificationResult(false, "****"));
    }
}
