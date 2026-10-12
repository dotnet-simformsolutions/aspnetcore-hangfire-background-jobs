using System.ComponentModel.DataAnnotations;

namespace HangfireOrderDemo.Models;

/// <summary>
/// The request body for POST /api/orders. The annotations are what
/// [ApiController] checks before the action runs, so an invalid body never
/// reaches the service or creates a job. On a record they must sit on the
/// constructor parameters, not on the generated properties.
/// </summary>
public record OrderRequest(
    [Required, EmailAddress] string CustomerEmail,
    [Required, StringLength(100, MinimumLength = 1)] string ProductName,
    [Range(1, 100)] int Quantity);
