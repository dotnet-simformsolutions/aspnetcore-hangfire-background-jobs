using Microsoft.AspNetCore.Mvc;
using HangfireOrderDemo.Models;
using HangfireOrderDemo.Services;

namespace HangfireOrderDemo.Controllers;

/// <summary>
/// HTTP only. [ApiController] validates the request body against the
/// annotations on <see cref="OrderRequest"/> and returns 400 before the action
/// runs; everything else is delegated to <see cref="IOrderService"/>.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class OrdersController(IOrderService orders) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult PlaceOrder([FromBody] OrderRequest request)
    {
        var result = orders.PlaceOrder(request);

        // Respond at once; none of the jobs has run yet.
        return Accepted(new
        {
            result.OrderId,
            paymentJob = result.PaymentJobId,
            reviewReminderIn = result.ReviewReminderDelay.ToString(),
            note = "Payment, confirmation, invoice and reminder run in the background."
        });
    }

    /// <summary>
    /// Queues a job that always throws, so the Retries and Failed tabs can be
    /// reached on demand without disturbing the happy path.
    /// </summary>
    [HttpPost("{id:int}/fail-demo")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public IActionResult FailDemo(int id)
    {
        var jobId = orders.QueueFailingJob(id);
        return Accepted(new { jobId, note = "This job throws on purpose." });
    }
}
