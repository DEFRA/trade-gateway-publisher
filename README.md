# trade-gateway-publisher

Core delivery C# ASP.NET backend template.

* [Install MongoDB](#install-mongodb)
* [Inspect MongoDB](#inspect-mongodb)
* [Testing](#testing)
* [Running](#running)
* [Dependabot](#dependabot)


### Docker Compose

A Docker Compose template is in [compose.yml](compose.yml).

A local environment with:

- Localstack for AWS services (S3, SQS)
- Redis
- MongoDB
- This service.
- A commented out frontend example.

```bash
docker compose up --build -d
```

A more extensive setup is available in [github.com/DEFRA/cdp-local-environment](https://github.com/DEFRA/cdp-local-environment)

### MongoDB

#### MongoDB via Docker

See above.

```
docker compose up -d mongodb
```

#### MongoDB locally

Alternatively install MongoDB locally:

- Install [MongoDB](https://www.mongodb.com/docs/manual/tutorial/#installation) on your local machine
- Start MongoDB:
```bash
sudo mongod --dbpath ~/mongodb-cdp
```

#### MongoDB in CDP environments

In CDP environments a MongoDB instance is already set up
and the credentials exposed as enviromment variables.


### Inspect MongoDB

To inspect the Database and Collections locally:
```bash
mongosh
```

You can use the CDP Terminal to access the environments' MongoDB.

### Testing

Run the tests with:

Tests run by running a full `WebApplication` backed by [Ephemeral MongoDB](https://github.com/asimmon/ephemeral-mongo).
Tests do not use mocking of any sort and read and write from the in-memory database.

```bash
dotnet test
````

### Running

Run CDP-Deployments application:
```bash
dotnet run --project TradeGatewayPublisher --launch-profile Development
```

### SonarCloud

Example SonarCloud configuration are available in the GitHub Action workflows.

### Dependabot

We have added an example dependabot configuration file to the repository. You can enable it by renaming
the [.github/example.dependabot.yml](.github/example.dependabot.yml) to `.github/dependabot.yml`


### About the licence

The Open Government Licence (OGL) was developed by the Controller of Her Majesty's Stationery Office (HMSO) to enable
information providers in the public sector to license the use and re-use of their information under a common open
licence.

It is designed to encourage use and re-use of information freely and flexibly, with only a few conditions.

# Message Deduplication

We publish to SNS FIFO topics, which require every message to carry a `MessageDeduplicationId`. SNS
drops a message whose id it has already seen within a 5-minute window. `ISnsPublisher` takes it as the
`duplicationId` parameter, or reads `IMessage.DuplicationId` on the generic overload.

**Most call sites currently pass a fresh `Guid`, so deduplication does nothing.** This is a
placeholder: it satisfies the topic's requirement for an id, but the id is never repeated. The one
exception is `FindChedUpdatesResponseRecord`, which keys on certificate `Id` alone — so it drops
genuinely newer updates to the same CHED inside the window.

The real key should identify *a version of a certificate* — likely `id + updated timestamp`, or a hash
of the body. It will be settled with the common message wrapper. Note `DuplicationId` is currently
serialised into the message body, so a body-hash key would be circular; the wrapper should carry it as
metadata.

`duplicationId` is optional, so it is easy to omit and fails at runtime rather than compile time. Every
publish call site must supply one, and each is pinned by a test asserting a non-empty id — see
`ChedUpdateConsumerTests`, `IntraUpdateConsumerTests` and `SnsPublisherTests`.

# Infrastructure.Leasing

A distributed lease provider backed by MongoDB, designed to ensure mutually exclusive execution of tasks across multiple application instances.

## Overview

The leasing system allows multiple instances of an application to coordinate work by acquiring named, time-bounded leases. Only one instance can hold a given lease at a time. When the holder is done, it releases the lease — or the lease expires automatically, preventing deadlocks if a holder crashes.

## Components

| Type | Description |
|---|---|
| `ILeaseProvider` | Interface for acquiring leases |
| `LeaseProvider` | MongoDB-backed implementation |
| `LeaseHandle` | Disposable handle that releases the lease on disposal |

## How It Works

1. A caller requests a lease by name and duration via `TryAcquireAsync`.
2. `LeaseProvider` attempts to insert a `LeaseEntity` document into MongoDB with a unique owner ID (`{MachineName}-{Guid}`).
3. If the insert succeeds, the caller receives a `LeaseHandle` and exclusively holds the lease.
4. If the insert fails (duplicate key), another instance already holds the lease — `null` is returned.
5. When the caller disposes the `LeaseHandle`, the lease document is deleted, freeing it for other instances.

Lease expiry is encoded in the `ExpiresAt` field of the stored document. Callers should use a TTL index on that field in MongoDB to automatically clean up abandoned leases.

## Usage

### Registration

Register the provider in your DI container:

```csharp
services.AddSingleton<ILeaseProvider, LeaseProvider>();
```

### Acquiring a Lease

```csharp
await using var lease = await _leaseProvider.TryAcquireAsync(
    leaseName: "my-background-job",
    duration: TimeSpan.FromMinutes(5),
    cancellationToken: cancellationToken);

if (lease is null)
{
    // Another instance holds the lease; skip this run.
    return;
}

// Lease is held — do exclusive work here.
// It is released automatically when `lease` is disposed.
```

### Lease Names

Use descriptive, stable names that identify the logical task being protected:

```
"reports:daily-summary"
"payments:settlement-run"
"cache:warm-up"
```

## Behaviour Reference

| Scenario | Result |
|---|---|
| Lease is free | Insert succeeds; `LeaseHandle` returned |
| Lease already held | Duplicate key exception caught; `null` returned |
| Unexpected error | Exception caught and logged as warning; `null` returned |
| Holder disposes handle | Lease document deleted; lease becomes available |
| Holder crashes | MongoDB TTL index removes document after `ExpiresAt` |

## Caveats

- **No renewal**: Leases cannot be extended. For long-running work, use a generous duration or implement a renewal loop.
- **No blocking wait**: `TryAcquireAsync` returns immediately. Callers that need to wait and retry must implement their own polling loop.
- **Clock skew**: Lease expiry relies on `DateTime.UtcNow` on the acquiring host. Ensure system clocks are synchronised across instances.


# Job Watermark Store

A lightweight infrastructure component for persisting and retrieving job watermarks in a MongoDB-backed store. Watermarks are used to track the last successfully processed point in time for background jobs, enabling efficient incremental processing and resumable job execution.

## Overview

The `JobWatermarkStore` provides a simple key-value interface keyed by job name, storing a `DateTimeOffset` value representing the last known good processing boundary. This allows scheduled or recurring jobs to avoid reprocessing already-handled data.

## Interface

```csharp
public interface IJobWatermarkStore
{
    Task<DateTimeOffset?> GetAsync(string jobName, CancellationToken cancellationToken = default);
    Task SetAsync(string jobName, DateTimeOffset watermark, CancellationToken cancellationToken = default);
}
```

### `GetAsync`

Retrieves the stored watermark for a given job.

| Parameter | Type | Description |
|---|---|---|
| `jobName` | `string` | The unique identifier for the job. |
| `cancellationToken` | `CancellationToken` | Optional cancellation token. |

**Returns:** `DateTimeOffset?` — the stored watermark in UTC, or `null` if no watermark exists yet.

### `SetAsync`

Persists (or updates) the watermark for a given job.

| Parameter | Type | Description |
|---|---|---|
| `jobName` | `string` | The unique identifier for the job. |
| `watermark` | `DateTimeOffset` | The new watermark value to store. |
| `cancellationToken` | `CancellationToken` | Optional cancellation token. |

The watermark is stored as a UTC `DateTime` and upserted — i.e. created if it does not exist, or updated in place if it does.

## Implementation Details

- **Storage:** Uses `IMongoCollectionSet<JobWatermarkEntity>`, backed by MongoDB via `IDbContext.Watermarks`.
- **Upsert behaviour:** `SetAsync` always overwrites the existing watermark for the job name, using the entity's `Id` field (set to `jobName`) as the document key.
- **Timezone handling:** Watermarks are stored as UTC `DateTime` values. On retrieval, the kind is explicitly set to `DateTimeKind.Utc` before wrapping in a `DateTimeOffset` to prevent timezone ambiguity.
- **Logging:**
  - `GetAsync` logs at `Information` level when no watermark is found, and at `Debug` level when one is successfully loaded.
  - `SetAsync` logs at `Information` level after a successful save.

## Usage

### Registration

Register the store in your DI container:

```csharp
services.AddScoped<IJobWatermarkStore, JobWatermarkStore>();
```

### Example: Incremental Job Processing

```csharp
public class MyBackgroundJob(IJobWatermarkStore watermarkStore)
{
    private const string JobName = "my-background-job";

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var since = await watermarkStore.GetAsync(JobName, cancellationToken)
                    ?? DateTimeOffset.UtcNow.AddDays(-1); // fallback for first run

        var newItems = await FetchItemsSinceAsync(since, cancellationToken);

        foreach (var item in newItems)
        {
            await ProcessAsync(item, cancellationToken);
        }

        await watermarkStore.SetAsync(JobName, DateTimeOffset.UtcNow, cancellationToken);
    }
}
```

## Dependencies

| Dependency | Purpose |
|---|---|
| `IDbContext` | Provides access to the MongoDB watermarks collection. |
| `ILogger<JobWatermarkStore>` | Structured logging via Microsoft.Extensions.Logging. |

## Namespace

```
Infrastructure.Watermark
```

# Infrastructure.Scheduler

A lightweight, extensible cron-job scheduling library for .NET hosted services. It runs jobs on configurable cron schedules with support for middleware pipelines, retry/backoff logic, concurrency control, and per-job settings via `appsettings.json`.

---

## Table of Contents

- [Features](#features)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Creating a Job](#creating-a-job)
- [Middleware](#middleware)
- [Job Context](#job-context)
- [Retry & Backoff](#retry--backoff)
- [Concurrency](#concurrency)
- [Architecture](#architecture)
- [API Reference](#api-reference)

---

## Features

- Cron-based scheduling with second-level precision (via [Cronos](https://github.com/HangfireIO/Cronos))
- Per-job settings: cron expression, max retries, retry delay
- Exponential backoff with jitter on failure
- Configurable max concurrent jobs via a semaphore
- ASP.NET Core-style middleware pipeline per job execution
- Typed `JobContext` for passing data between middleware and job handlers
- `WatermarkContext` support for time-windowed jobs
- DI-friendly: jobs and middleware registered via the service container

---

## Getting Started

### 1. Register the scheduler

```csharp
// Program.cs
builder.Services.AddScheduler(builder.Configuration);
```

### 2. Implement `ICronJob`

```csharp
public class MyJob : ICronJob
{
    public string Name => "MyJob";

    public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        // job logic here
        return Task.CompletedTask;
    }
}
```

### 3. Register your jobs

```csharp
builder.Services.AddScoped<ICronJob, MyJob>();
```

### 4. Add configuration

```json
{
  "Scheduler": {
    "MaxConcurrentJobs": 2,
    "Jobs": {
      "MyJob": {
        "Cron": "0 * * * * *",
        "MaxRetries": 3,
        "RetryDelaySeconds": 2
      }
    }
  }
}
```

---

## Configuration

Settings are bound from `appsettings.json` under the `Scheduler` section.

### `SchedulerSettings`

| Property | Type | Default | Description |
|---|---|---|---|
| `MaxConcurrentJobs` | `int` | `1` | Maximum number of jobs allowed to run simultaneously |
| `Jobs` | `Dictionary<string, JobSettings>` | `{}` | Per-job configuration keyed by `ICronJob.Name` |

### `JobSettings`

| Property | Type | Default | Description |
|---|---|---|---|
| `Cron` | `string` | `"* * * * *"` | Cron expression (second-precision supported) |
| `MaxRetries` | `int` | `3` | Maximum retry attempts after initial failure |
| `RetryDelaySeconds` | `int` | `2` | Base delay (seconds) before the first retry |

#### Cron Format

The scheduler uses [Cronos](https://github.com/HangfireIO/Cronos) with `CronFormat.IncludeSeconds`, so expressions have six fields:

```
┌─────────── second (0–59)
│ ┌───────── minute (0–59)
│ │ ┌─────── hour (0–23)
│ │ │ ┌───── day of month (1–31)
│ │ │ │ ┌─── month (1–12)
│ │ │ │ │ ┌─ day of week (0–7, Sun=0 or 7)
│ │ │ │ │ │
* * * * * *
```

Examples:

| Expression | Meaning |
|---|---|
| `0 * * * * *` | Every minute, on the hour |
| `0 0 * * * *` | Every hour |
| `0 0 9 * * 1` | Every Monday at 09:00 |
| `*/30 * * * * *` | Every 30 seconds |

---

## Creating a Job

Implement `ICronJob` and register it as a scoped service:

```csharp
public class ReportGeneratorJob : ICronJob
{
    private readonly IReportService _reportService;

    public ReportGeneratorJob(IReportService reportService)
    {
        _reportService = reportService;
    }

    public string Name => "ReportGenerator";

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        // Optionally read data placed by middleware
        var watermark = context.Get<WatermarkContext>();

        await _reportService.GenerateAsync(cancellationToken);
    }
}
```

Register:

```csharp
services.AddScoped<ICronJob, ReportGeneratorJob>();
```

> **Note:** Every job's `Name` property must match a key in `SchedulerSettings.Jobs` in configuration.

---

## Middleware

Middleware allows you to wrap job execution with cross-cutting concerns such as logging, tracing, locking, or watermark management. The pipeline mirrors the ASP.NET Core middleware model.

### Implementing middleware

```csharp
public class LoggingMiddleware : IJobMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        _logger.LogInformation("Starting job {Name} ({JobId})", context.Name, context.JobId);

        await next(context, cancellationToken);

        _logger.LogInformation("Completed job {Name} ({JobId})", context.Name, context.JobId);
    }
}
```

### Registering middleware

```csharp
services.AddScoped<IJobMiddleware, LoggingMiddleware>();
```

Middleware is executed in **reverse registration order**, so the last registered middleware wraps the outermost layer of the pipeline.

### Built-in middleware integrations

The `ServiceCollectionExtensions` wires up:

- `IJobWatermarkStore` — for tracking job high-water marks
- `ILeaseProvider` — for distributed locking (preventing duplicate runs across instances)

---

## Job Context

`JobContext` is a typed property bag scoped to a single job execution. It is passed through the entire middleware pipeline and into the job's `ExecuteAsync` method.

```csharp
// Set a value (e.g. in middleware)
context.Set(new WatermarkContext(lastRun, DateTimeOffset.Now));

// Read it in the job
var wm = context.Get<WatermarkContext>();        // returns null if missing
var wm = context.GetRequired<WatermarkContext>(); // throws if missing

// Check existence
if (context.TryGet<WatermarkContext>(out var wm)) { ... }

// Remove a value
context.Remove<WatermarkContext>();
```

Each context instance also carries:

| Property | Description |
|---|---|
| `JobId` | A unique UUIDv7 string generated per execution |
| `Name` | The job name from `ICronJob.Name` |

---

## Retry & Backoff

When a job throws an unhandled exception, `JobExecutor` automatically retries up to `MaxRetries` times using **exponential backoff with jitter**:

```
delay = min(baseSeconds × 2^(attempt-1) + jitter, 2 minutes)
```

- `baseSeconds` comes from `JobSettings.RetryDelaySeconds` (minimum 1 second)
- Jitter is a random value between 100ms and 500ms to avoid thundering herd
- The backoff is capped at **2 minutes** regardless of attempt count

With the default settings (`MaxRetries: 3`, `RetryDelaySeconds: 2`):

| Attempt | Approximate delay before next retry |
|---|---|
| 1 → 2 | ~2s |
| 2 → 3 | ~4s |
| 3 → 4 | ~8s |
| 4 | Final failure — exception propagates |

`OperationCanceledException` raised due to cancellation is never retried and propagates immediately.

---

## Concurrency

The `MaxConcurrentJobs` setting (default: `1`) limits how many jobs can run simultaneously across the scheduler. A `SemaphoreSlim` is used internally; if the semaphore cannot be acquired (e.g. during shutdown), the job run is skipped gracefully with a warning log rather than blocking.

To allow full parallelism, set `MaxConcurrentJobs` to the total number of registered jobs.

---

## Architecture

```
SchedulerBackgroundService          (IHostedService — ticks every second)
    │
    ├── per tick: checks each job's next scheduled time
    │
    └── RunJobAsync(jobName)
            │
            ├── acquires semaphore slot
            ├── resolves ICronJob + IJobExecutor from a fresh DI scope
            │
            └── JobExecutor.ExecuteAsync(job, settings)
                    │
                    ├── creates JobContext (unique JobId)
                    ├── retry loop (up to MaxRetries + 1 attempts)
                    │
                    └── ExecutePipelineAsync
                            │
                            ├── IJobMiddleware (n)  ─┐
                            ├── IJobMiddleware (n-1)  │  pipeline built
                            ├── ...                   │  in reverse order
                            └── ICronJob.ExecuteAsync ┘
```

Each job run creates its own DI scope, so scoped services (repositories, DbContext, etc.) are safely isolated per execution.

---

### Queue Architecture

```mermaid

sequenceDiagram
    participant TGPublisher as Trade Gateway Publisher
    participant TradeGateway as Trade Gateway
    
    box rgb(255, 153, 0, 0.3) Amazon SNS
      participant StreamTopic as Certificate Summary Update SNS Topic
      participant StreamQueue@{ "type" : "queue" } as Certificate Summary Update Queue
      participant SingleUpdateTopic as Single Update SNS Topic
      participant SingleUpdateQueue@{ "type" : "queue" } as Single Certificate Update Queue (CDP Consumers)
      participant SNSAsbQueue@{ "type" : "queue" } as Single Certificate Update Queue (for ASB Topic)
    end

    box rgb(60, 203, 244, 0.3) Azure Service Bus
       participant AsbTopic as Asb Topic
    end
    TGPublisher->>TradeGateway: Find Update List
    TradeGateway->>TGPublisher: Update List
        
    loop For Each Certificate Update
        TGPublisher->>StreamTopic:Update Notification
        StreamTopic->>StreamQueue: Update Notification

        StreamQueue-->>TGPublisher: Update event

        TGPublisher->>TradeGateway: Get Full Ched
        TradeGateway->>TGPublisher: Full Ched 

        TGPublisher->>SingleUpdateTopic: Post Full Ched
        SingleUpdateTopic->>SingleUpdateQueue: Receive Full Ched

        SingleUpdateTopic->>SNSAsbQueue: Receive Full Ched

        SNSAsbQueue-->>TGPublisher:Receive Full Ched

        TGPublisher->>AsbTopic: Publish to ASB Topic
    end

    %%{init:{'themeCSS':'.actor[data-id=StreamQueue], .actor[data-id=SNSAsbQueue] { fill: rgb(255, 153, 0); };.actor[data-id=SingleUpdateQueue]{ fill: rgb(255, 255, 0); } '}}%%
 
 ```

|Entity|Update Summary SNS topic|Update Summary SMS Queue|Single Update SNS topic|Single Update SNS Queue (CDP Consumers)|Single Update SNS Queue (For Azure Publish)|Service Bus Topic|
|-|-|-|-|-|-|-|
|CHED|trade_gateway_publisher_ched_stream_internal|trade_gateway_publisher_ched_stream_internal_publisher|trade_gateway_publisher_ched_updates|*|trade_gateway_publisher_ched_stream_internal_asb_publisher|trade-gateway-publisher-ched|
|INTRA|trade_gateway_publisher_intra_stream_internal|trade_gateway_publisher_intra_stream_internal_publisher|trade_gateway_publisher_intra_updates|*|trade_gateway_publisher_intra_stream_internal_asb_publisher|trade-gateway-publisher-intra|
|DOCOM|-|-|-|-|-|-|

***NOTE*** Single Update SNS Queue (CDP Consumers) would be created for a CDP consumer, for integration tests it is represented by trade_gateway_publisher_{certificate-type}_updates_test

## API Reference

### `ICronJob`

| Member | Description |
|---|---|
| `string Name` | Unique identifier; must match a key in `SchedulerSettings.Jobs` |
| `Task ExecuteAsync(JobContext, CancellationToken)` | Job entry point |

### `IJobMiddleware`

| Member | Description |
|---|---|
| `Task InvokeAsync(JobContext, CancellationToken, JobExecutionDelegate next)` | Wrap the pipeline; call `next` to continue |

### `IJobExecutor`

| Member | Description |
|---|---|
| `Task ExecuteAsync(ICronJob, JobSettings, CancellationToken)` | Runs the full middleware pipeline with retry logic |

### `JobContext`

| Member | Description |
|---|---|
| `string JobId` | UUIDv7 unique to this execution |
| `string Name` | Job name |
| `void Set<T>(T value)` | Store a typed value |
| `T? Get<T>()` | Retrieve a typed value, or null |
| `T GetRequired<T>()` | Retrieve a typed value, throws if absent |
| `bool TryGet<T>(out T? value)` | Try-get pattern |
| `bool Remove<T>()` | Remove a typed value |

### `WatermarkContext`

| Member | Description |
|---|---|
| `DateTimeOffset Watermark` | The previous high-water mark (last successful run boundary) |
| `DateTimeOffset Now` | The current execution time |

### `JobExecutionDelegate`

```csharp
public delegate Task JobExecutionDelegate(JobContext context, CancellationToken cancellationToken);
```

Represents the next step in the middleware pipeline or the final job handler.