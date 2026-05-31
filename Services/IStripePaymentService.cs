namespace CoffeeShopApi.Services;

public interface IStripePaymentService
{
    Task<string> CreatePaymentIntentAsync(long amountInCents, string currency);
    Task<PaymentVerificationResult> VerifyPaymentIntentAsync(string paymentIntentId);
}

public record PaymentVerificationResult(bool IsSucceeded, string CardLastFour);
