using Hangfire;
using Hangfire.Dashboard;
using HangfireOrderDemo.Security;
using HangfireOrderDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Hangfire and choose where jobs are stored.
builder.Services.AddHangfire(config =>
{
    // Hangfire's recommended settings: the newest data format, with short type
    // names so stored jobs still load after you release a new version.
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
          .UseSimpleAssemblyNameTypeSerializer()
          .UseRecommendedSerializerSettings();

    if (builder.Configuration["Storage:Provider"] == "InMemory")
    {
        // Local runs: nothing to install, but everything is lost on restart.
        config.UseInMemoryStorage();
    }
    else
    {
        var connectionString = builder.Configuration.GetConnectionString("HangfireConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:HangfireConnection, or set Storage:Provider to \"InMemory\". " +
                "Hangfire creates its own tables, but it does not create the database.");
        }

        config.UseSqlServerStorage(connectionString);
    }
});

// 2. Start the Hangfire server that runs the jobs. Every copy of this app that
//    calls this line becomes a worker.
builder.Services.AddHangfireServer();

// 3. Register the services the jobs use. The order store is a singleton,
//    because it has to outlive the request that wrote to it.
builder.Services.AddSingleton<IOrderStore, InMemoryOrderStore>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IReportService, ReportService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The dashboard's retry, delete and trigger buttons send POST requests.
// Registering antiforgery makes Hangfire validate them; a Web API project
// does not register it on its own.
builder.Services.AddAntiforgery();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // Development: Hangfire's built-in check already limits the dashboard to
    // requests from this machine, so there is nothing to sign in to.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "HangfireOrderDemo (Development)",
        DisplayStorageConnectionString = false
    });
}
else
{
    // Production: only signed-in Support and Admin users can open it.
    // HangfireDashboardAuthFilter denies everyone until you add authentication
    // (cookie, OpenID Connect, ...) and call UseAuthentication/UseAuthorization
    // above this line.
    //
    // Do NOT rely on the built-in local-only check here: behind a reverse
    // proxy every request can look like it came from this machine.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        DashboardTitle = "HangfireOrderDemo",
        DisplayStorageConnectionString = false,
        Authorization = new[] { new HangfireDashboardAuthFilter() },
        // Support can view jobs; only Admin can retry or delete them.
        IsReadOnlyFunc = context => !context.GetHttpContext().User.IsInRole("Admin")
    });
}

app.MapControllers();

// 4. Register the nightly report. Resolved through DI, so it does not depend
//    on anything else having initialised Hangfire's static storage first.
//    Safe to run on every instance: it is an upsert protected by a lock.
app.Services.GetRequiredService<IRecurringJobManager>()
    .AddOrUpdate<IReportService>(
        "daily-sales-report",
        report => report.SendDailySalesReport(CancellationToken.None),
        Cron.Daily(2));   // 02:00 UTC — the time zone defaults to UTC

app.Run();
