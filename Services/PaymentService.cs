using System.Collections.Concurrent;
using Hangfire;

namespace HangfireOrderDemo.Services;

public interface IPaymentService
{
    // Hangfire attributes must sit on the INTERFACE. The controller enqueues
    // through IPaymentService, so that is the type Hangfire stores and reads
    // filters from. The same attribute on PaymentService would be ignored.
    [AutomaticRetry(Attempts = 3)]
    Task CapturePayment(int orderId);
}

public class PaymentService(IConfiguration config, ILogger<PaymentService> logger) : IPaymentService
{
    // Static, because the service is scoped: Hangfire creates a new instance
    // for every attempt, so an instance field would never see attempt two.
    private static readonly ConcurrentDictionary<int, int> Attempts = new();

    public Task CapturePayment(int orderId)
    {
        var failuresToFake = config.GetValue("Demo:FlakyPaymentFailures", 2);
        var attempt = Attempts.AddOrUpdate(orderId, 1, (_, previous) => previous + 1);

        // Demo-only: fail the first few attempts so the retry ladder and the
        // Retries tab have something to show.
        if (attempt <= failuresToFake)
        {
            logger.LogWarning(
                "Payment attempt {Attempt} for order {OrderId} failed", attempt, orderId);
            throw new HttpRequestException($"Payment gateway timed out for order {orderId}.");
        }

        logger.LogInformation(
            "Payment captured for order {OrderId} on attempt {Attempt}", orderId, attempt);
        return Task.CompletedTask;
    }
}
