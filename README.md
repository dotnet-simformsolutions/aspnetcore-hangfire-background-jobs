# HangfireOrderDemo

A small ASP.NET Core Web API showing **background jobs with [Hangfire](https://www.hangfire.io/)**
through a realistic order flow. It is the companion to the article
*Background Jobs in ASP.NET Core Using Hangfire*, and the article's code samples come from here.

One `POST /api/orders` uses all four Hangfire job types, and the sample is built to show what
goes wrong in production as well as what goes right: retries, continuations that stall, jobs
that run twice, and a dashboard that must not be left open.

## Quick start

Needs the **.NET 10 SDK**. No database required: Development uses in-memory storage.

```bash
git clone https://github.com/hardik2015/HangfireOrderDemo
cd HangfireOrderDemo/HangfireOrderDemo
dotnet run
```

Then place an order:

```bash
curl -k -X POST https://localhost:7001/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerEmail":"sam@example.com","productName":"Wireless Mouse","quantity":2}'
```

Open the dashboard at **https://localhost:7001/hangfire**. No sign-in is needed on your own
machine — Hangfire's built-in check already limits the dashboard to local requests.

## What you will see

The API replies immediately, then, over the next minute or so:

| Dashboard tab | What appears there |
|---|---|
| **Retries** | `IPaymentService.CapturePayment` — it fails twice on purpose, then succeeds on the third attempt |
| **Awaiting** | the confirmation and invoice jobs, waiting for the payment to succeed |
| **Scheduled** | `IEmailService.SendReviewReminder`, due 60 seconds later |
| **Succeeded** | all four jobs, once the chain completes |
| **Recurring Jobs** | `daily-sales-report`, due at 02:00 **UTC** |

A real run looks like this in the console:

```text
warn: PaymentService  Payment attempt 1 for order 1 failed
warn: AutomaticRetry  Retry attempt 1 of 3 will be performed in 00:00:27.
warn: PaymentService  Payment attempt 2 for order 1 failed
warn: AutomaticRetry  Retry attempt 2 of 3 will be performed in 00:00:34.
info: EmailService    Review reminder for order 1 sent
info: PaymentService  Payment captured for order 1 on attempt 3
info: EmailService    Confirmation for order 1 sent
info: InvoiceService  Invoice generated for order 1: 2 x Wireless Mouse
```

## The four job types

| Job type | Hangfire call | In this sample |
|---|---|---|
| Fire-and-forget | `Enqueue(...)` | Capture the payment |
| Continuation | `ContinueJobWith(...)` | Send the confirmation once payment succeeds, then the invoice |
| Delayed | `Schedule(..., delay)` | Review reminder — **60 seconds in Development, 3 days by default** |
| Recurring | `IRecurringJobManager.AddOrUpdate(...)` | Nightly sales report at 02:00 UTC |

The reminder delay comes from `Demo:ReviewReminderDelay`. It is **demo configuration**:
`appsettings.Development.json` sets it to `00:01:00` so you can watch it run. The key is absent
from `appsettings.json`, so anything other than Development gets the realistic 3 days.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/api/orders` | Place an order and queue the four jobs. An invalid body returns `400` before any job is created |
| `POST` | `/api/orders/{id}/fail-demo` | Queue a job that always throws, to reach the Retries and Failed tabs |
| `GET` | `/api/jobs/health` | Queue depth, retries, and continuations still awaiting |
| `GET` | `/hangfire` | The dashboard |
| `GET` | `/swagger` | Swagger UI (Development only) |

`HangfireOrderDemo.http` has all of these ready to run from Visual Studio or VS Code.

## Storage: in-memory or SQL Server

Storage is chosen by the `Storage:Provider` setting:

| Value | Behaviour |
|---|---|
| `InMemory` | the Development default. Nothing to install, but **everything is lost when the app stops** |
| `SqlServer` | reads `ConnectionStrings:HangfireConnection` |

To use SQL Server, **create the database first** — Hangfire creates its own tables, but it does
not create the database:

```sql
CREATE DATABASE HangfireOrderDemo;
```

Then set both values, for example with LocalDB:

```json
{
  "Storage": { "Provider": "SqlServer" },
  "ConnectionStrings": {
    "HangfireConnection": "Server=(localdb)\\MSSQLLocalDB;Database=HangfireOrderDemo;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

`TrustServerCertificate=True` is for local development only.

## Break it on purpose

The sample is more useful broken than working:

- `POST /api/orders/{id}/fail-demo` queues a job that always throws. Watch it move through
  Retries and land in **Failed** — where it stays, because Hangfire never deletes failed jobs.
- Set `Demo:FlakyPaymentFailures` higher than the retry count, and the payment fails for good.
  The confirmation and invoice then sit in **Awaiting** forever: a continuation only runs when
  its parent *succeeds*, and nothing warns you.
- Stop the app mid-job and start it again. With `SqlServer` storage the job comes back; with
  in-memory storage it is gone.
- Run two copies against the same SQL Server database and watch the recurring job still run
  once per schedule.

## Production notes

The sample is deliberately simple. Before copying it into a real system:

- **The controller does not orchestrate jobs.** `OrdersController` validates the request and
  calls `IOrderService`. `OrderService` saves the order through `IOrderStore`, then creates the
  payment, continuation, delayed and reminder jobs. Keeping that sequence in one place is also
  where a transaction, an outbox or a reconciliation job would go.
- **Secure the dashboard.** In Development, Hangfire's local-only check is enough. Outside it,
  `Security/HangfireDashboardAuthFilter.cs` is wired up and denies everyone until you add
  authentication. Do not rely on the local-only check in production: behind a reverse proxy the
  remote address is the proxy's, so every request can look local.
- **Retry limits are per job.** The framework default is 10 retries spread over roughly four to
  five hours. `[AutomaticRetry(Attempts = 3)]` here is a deliberate choice.
- **Attributes go on the interface.** Jobs are enqueued through `IPaymentService`, so that is
  where Hangfire reads `[AutomaticRetry]` from. On the implementation class it is ignored.
- **Jobs can run twice.** `EmailService.SendOrderConfirmation` checks `WasConfirmationSent`,
  sends, and only then calls `MarkConfirmationSent`. Recording the send first would turn a
  failed send into a skipped one. The remaining duplicate window is what a provider
  idempotency key is for.
- **A missing order fails the job.** The email jobs throw rather than return, so the failure
  reaches the Retries and Failed tabs instead of being reported as success.
- **No personal data in job arguments.** Jobs take an `orderId` and look the customer up through
  `IOrderStore`. Hangfire stores arguments as plain text and shows them in the dashboard.
- **Succeeded jobs are deleted after one day**, so the dashboard is not an audit log.

## Project layout

```
HangfireOrderDemo/
├── Program.cs                          # Hangfire setup, dashboard, recurring job
├── appsettings.json                    # Storage:Provider, connection string
├── appsettings.Development.json        # InMemory storage + demo timings
├── Controllers/
│   ├── OrdersController.cs             # HTTP only: validate, then call IOrderService
│   └── JobsController.cs               # GET /api/jobs/health
├── Services/
│   ├── OrderService.cs                 # application service: saves the order, then creates the jobs
│   ├── OrderStore.cs                   # stands in for a database; idempotency guard
│   ├── PaymentService.cs               # fails twice on purpose, then succeeds
│   ├── EmailService.cs                 # confirmation + review reminder
│   ├── InvoiceService.cs               # invoice, plus an always-failing demo job
│   └── ReportService.cs                # nightly report, observes CancellationToken
├── Models/OrderRequest.cs
└── Security/HangfireDashboardAuthFilter.cs
```

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| `Cannot open database "HangfireOrderDemo"` | The database does not exist. Hangfire creates tables, not databases — run `CREATE DATABASE` first. |
| Startup throws `Set ConnectionStrings:HangfireConnection...` | `Storage:Provider` is `SqlServer` with an empty connection string. Set one, or switch to `InMemory`. |
| Jobs stay in **Enqueued** and never run | No Hangfire server is running — something removed `AddHangfireServer()`, or the server listens on a different queue. |
| The dashboard is blank or returns 401 | You are not on the local machine, and outside Development the auth filter denies everyone until authentication is configured. |
| `Please add a NuGet package reference to ... SqlClient` | `Hangfire.SqlServer` ships no SQL client. Keep the `Microsoft.Data.SqlClient` reference. |
| Everything disappeared after a restart | Development uses in-memory storage. Switch `Storage:Provider` to `SqlServer`. |

## Versions

.NET 10, Hangfire 1.8.25, Hangfire.InMemory 1.0.0, Swashbuckle 10.2.3.

## Further reading

The companion article covers the production topics this sample only gestures at: choosing
storage, securing the dashboard, retry behaviour, idempotency, queues and workers, keeping
personal data out of job arguments, retention, and running workers on separate servers.
