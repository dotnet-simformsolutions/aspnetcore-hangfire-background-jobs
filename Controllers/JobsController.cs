using Hangfire;
using Hangfire.Storage;
using Microsoft.AspNetCore.Mvc;

namespace HangfireOrderDemo.Controllers;

/// <summary>
/// The numbers worth alerting on, without screen-scraping the dashboard.
/// JobStorage is injected rather than taken from the JobStorage.Current
/// static, which is ambient state a test cannot substitute.
/// </summary>
[ApiController]
[Route("api/jobs")]
public class JobsController(JobStorage storage) : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health()
    {
        IMonitoringApi api = storage.GetMonitoringApi();
        var stats = api.GetStatistics();

        return Ok(new
        {
            Enqueued = api.EnqueuedCount("default"),
            Scheduled = api.ScheduledCount(),
            Processing = api.ProcessingCount(),
            Failed = api.FailedCount(),
            // A failing job sits in Scheduled for hours before it reaches
            // Failed, so retries are the earlier warning.
            Retries = stats.Retries,
            // Continuations whose parent failed wait here forever.
            Awaiting = stats.Awaiting,
            Servers = api.Servers().Count
        });
    }
}
