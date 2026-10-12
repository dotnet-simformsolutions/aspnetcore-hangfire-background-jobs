using Hangfire;

namespace HangfireOrderDemo.Services;

public interface IEmailService
{
    // Only the order id is passed. The email address is looked up inside the
    // job, so no personal data is stored in Hangfire's database.
    [AutomaticRetry(Attempts = 3)]
    Task SendOrderConfirmation(int orderId);

    [AutomaticRetry(Attempts = 3)]
    Task SendReviewReminder(int orderId);
}

public class EmailService(IOrderStore orders, ILogger<EmailService> logger) : IEmailService
{
    public Task SendOrderConfirmation(int orderId)
    {
        // If the order is gone, fail the job. Returning quietly would report
        // success for a confirmation that was never sent.
        var order = orders.Get(orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        // A job can run more than once, so skip the send if an earlier attempt
        // already finished it.
        if (orders.WasConfirmationSent(orderId))
        {
            logger.LogInformation("Order {OrderId} already confirmed; skipping", orderId);
            return Task.CompletedTask;
        }

        // TODO: integrate a real email provider (SendGrid, SMTP, ...):
        //   provider.Send(order.CustomerEmail, key: $"order-confirmation-{orderId}");
        // The key lets the provider count repeats of the same send once.
        // Never log the address: see "Keeping PII Out of Job Storage".

        // Record the send only after it succeeded. Marking first would turn a
        // failed send into a skipped one, and the customer would get nothing.
        orders.MarkConfirmationSent(orderId);
        logger.LogInformation("Confirmation for order {OrderId} sent", orderId);
        return Task.CompletedTask;
    }

    public Task SendReviewReminder(int orderId)
    {
        var order = orders.Get(orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        // TODO: provider.Send(order.CustomerEmail, ...) — the address stays out
        // of the log line below.
        logger.LogInformation("Review reminder for order {OrderId} sent", orderId);
        return Task.CompletedTask;
    }
}
