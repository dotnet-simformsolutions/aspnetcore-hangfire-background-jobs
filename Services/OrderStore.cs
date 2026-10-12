using System.Collections.Concurrent;
using HangfireOrderDemo.Models;

namespace HangfireOrderDemo.Services;

public record Order(int Id, string CustomerEmail, string ProductName, int Quantity);

public interface IOrderStore
{
    int Add(OrderRequest request);
    Order? Get(int orderId);

    /// <summary>
    /// True once a confirmation has actually been sent for this order.
    /// </summary>
    bool WasConfirmationSent(int orderId);

    /// <summary>
    /// Called after the send succeeds, never before it. Checking and marking
    /// are two steps, so two attempts running at the same moment can still
    /// both send: that is at-least-once, and the provider's own idempotency
    /// key is what removes the duplicate. Marking first would be worse — a
    /// send that failed afterwards would be skipped on the retry.
    /// </summary>
    void MarkConfirmationSent(int orderId);
}

/// <summary>
/// Stands in for a real database so the sample runs with no setup.
/// Registered as a singleton, because a job reads an order minutes or days
/// after the request that created it has finished.
/// </summary>
public class InMemoryOrderStore : IOrderStore
{
    private readonly ConcurrentDictionary<int, Order> _orders = new();
    private readonly ConcurrentDictionary<int, byte> _confirmationSent = new();
    private int _nextId;

    public int Add(OrderRequest request)
    {
        var id = Interlocked.Increment(ref _nextId);
        _orders[id] = new Order(id, request.CustomerEmail, request.ProductName, request.Quantity);
        return id;
    }

    public Order? Get(int orderId) =>
        _orders.TryGetValue(orderId, out var order) ? order : null;

    public bool WasConfirmationSent(int orderId) =>
        _confirmationSent.ContainsKey(orderId);

    public void MarkConfirmationSent(int orderId) =>
        _confirmationSent[orderId] = 0;
}
