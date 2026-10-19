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
            // How long the oldest job has been waiting for a worker. Queue
            // length can hold steady while this climbs, so it is the clearest
            // sign that workers are falling behind. Null when nothing waits.
            OldestEnqueuedJobAgeSeconds = OldestEnqueuedJobAgeSeconds(api),
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

    private static double? OldestEnqueuedJobAgeSeconds(IMonitoringApi api)
    {
        // Each queue lists its first few jobs, oldest first, across every
        // queue a server might listen to.
        DateTime? oldest = api.Queues()
            .SelectMany(queue => queue.FirstJobs)
            .Select(job => job.Value)
            .Where(job => job is { InEnqueuedState: true })
            .Min(job => job!.EnqueuedAt);

        return oldest is null
            ? null
            : Math.Round((DateTime.UtcNow - oldest.Value).TotalSeconds, 1);
    }
}
