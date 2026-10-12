using Hangfire;

namespace HangfireOrderDemo.Services;

public interface IInvoiceService
{
    void GenerateInvoice(int orderId);

    /// <summary>
    /// Always throws. Lets you reach the Retries and Failed tabs on demand
    /// without breaking the happy path.
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    void FailOnPurpose(int orderId);
}

public class InvoiceService(IOrderStore orders, ILogger<InvoiceService> logger) : IInvoiceService
{
    public void GenerateInvoice(int orderId)
    {
        // Fail the job rather than return: an invoice that was never generated
        // must not be recorded as a success.
        var order = orders.Get(orderId)
            ?? throw new InvalidOperationException($"Order {orderId} not found.");

        // TODO: generate a real PDF here.
        logger.LogInformation(
            "Invoice generated for order {OrderId}: {Quantity} x {Product}",
            orderId, order.Quantity, order.ProductName);
    }

    public void FailOnPurpose(int orderId) =>
        throw new InvalidOperationException(
            $"Invoice generation for order {orderId} failed on purpose (demo endpoint).");
}
