using Hangfire;
using HangfireOrderDemo.Models;

namespace HangfireOrderDemo.Services;

/// <summary>What the caller is told once an order has been accepted.</summary>
public record PlaceOrderResult(int OrderId, string PaymentJobId, TimeSpan ReviewReminderDelay);

public interface IOrderService
{
    PlaceOrderResult PlaceOrder(OrderRequest request);

    /// <summary>Queues a job that always throws, to reach the Retries and Failed tabs.</summary>
    string QueueFailingJob(int orderId);
}

/// <summary>
/// The application service for orders. It owns the order of operations —
/// persist first, then create the jobs — and nothing in here knows about HTTP.
/// The controller validates its input and calls this; the job wiring lives in
/// one place, which is also the place to add a transaction or an outbox later.
/// </summary>
public class OrderService(
    IBackgroundJobClient jobs,
    IOrderStore orders,
    IConfiguration config) : IOrderService
{
    private static readonly TimeSpan DefaultReviewReminderDelay = TimeSpan.FromDays(3);

    public PlaceOrderResult PlaceOrder(OrderRequest request)
    {
        // Save the order BEFORE enqueuing anything. A worker can start a job
        // within a fraction of a second, and the order must exist by then.
        var orderId = orders.Add(request);

        // Fire-and-forget: capture the payment now.
        var paymentJob = jobs.Enqueue<IPaymentService>(p => p.CapturePayment(orderId));

        // Continuations: each runs only after the previous job succeeds.
        var emailJob = jobs.ContinueJobWith<IEmailService>(
            paymentJob, e => e.SendOrderConfirmation(orderId));
        jobs.ContinueJobWith<IInvoiceService>(emailJob, i => i.GenerateInvoice(orderId));

        // Delayed: 3 days by default; Development overrides this to 60 seconds
        // so the Scheduled tab is observable in one sitting.
        var delay = config.GetValue<TimeSpan?>("Demo:ReviewReminderDelay")
                    ?? DefaultReviewReminderDelay;
        jobs.Schedule<IEmailService>(e => e.SendReviewReminder(orderId), delay);

        return new PlaceOrderResult(orderId, paymentJob, delay);
    }

    public string QueueFailingJob(int orderId) =>
        jobs.Enqueue<IInvoiceService>(i => i.FailOnPurpose(orderId));
}
