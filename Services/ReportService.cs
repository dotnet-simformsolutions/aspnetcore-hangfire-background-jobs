using Hangfire;

namespace HangfireOrderDemo.Services;

public interface IReportService
{
    // Only one report runs at a time, even when several servers share the
    // same storage. The attribute goes on the interface, like the others.
    [DisableConcurrentExecution(timeoutInSeconds: 300)]
    Task SendDailySalesReport(CancellationToken cancellationToken);
}

public class ReportService(ILogger<ReportService> logger) : IReportService
{
    private static readonly string[] Regions = ["EU", "US", "APAC"];

    public async Task SendDailySalesReport(CancellationToken cancellationToken)
    {
        foreach (var region in Regions)
        {
            // Check between regions, so a deployment stops the job at a point
            // we chose rather than in the middle of one.
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(200, cancellationToken);
            logger.LogInformation("Daily sales report sent for {Region}", region);
        }
    }
}
