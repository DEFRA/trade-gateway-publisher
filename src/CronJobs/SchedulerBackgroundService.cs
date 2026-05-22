using System.Text.Json;
using Cronos;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CronJobs;

public sealed class SchedulerBackgroundService(
    IEnumerable<ICronJob> cronJobs,
    IJobExecutor jobExecutor,
    IOptions<SchedulerSettings> settings,
    ILogger<SchedulerBackgroundService> logger
) : BackgroundService
{
    private readonly SchedulerSettings _settings = settings.Value ?? new SchedulerSettings()
    {
        Jobs = new Dictionary<string, JobSettings>()
        {
            ["ExampleJob"] = new() { Cron = "* * * * *"}
        }
    };

    private readonly List<(ICronJob Job, CronExpression Expression)> _jobs =
    [
        .. cronJobs.Select(job =>
            (Job: job, Expression: CronExpression.Parse(settings.Value?.Jobs[job.Name].Cron ?? string.Empty))
        ),
    ];

    private readonly TimeZoneInfo _timeZoneInfo = TimeZoneInfo.Local;

    private readonly SemaphoreSlim _semaphore = new(settings.Value!.MaxConcurrentJobs);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextRuns = _jobs.ToDictionary(
            x => x.Job.Name,
            x => x.Expression.GetNextOccurrence(DateTimeOffset.Now, _timeZoneInfo)
        );

        logger.LogInformation(
            "Scheduler started with {JobCount} jobs and max concurrency {MaxConcurrency}",
            _jobs.Count,
            _settings.MaxConcurrentJobs
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.Now;

            foreach (var (job, expression) in _jobs)
            {
                var next = nextRuns[job.Name];

                if (!next.HasValue || next.Value > now)
                {
                    continue;
                }

                _ = RunJobAsync(job, stoppingToken);

                nextRuns[job.Name] = expression.GetNextOccurrence(now.AddSeconds(1), _timeZoneInfo);

                logger.LogInformation("Next run of {Job} scheduled at {NextRun}", job.Name, nextRuns[job.Name]);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Scheduler stopped");
    }

    private async Task RunJobAsync(ICronJob job, CancellationToken cancellationToken)
    {
        var acquired = false;

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            acquired = true;

            logger.LogInformation("Running job {Job}", job.Name);

            var jobSettings = _settings.Jobs[job.Name];

            await jobExecutor.ExecuteAsync(job, jobSettings, cancellationToken);
        }
        ////catch (OperationCanceledException)
        ////{
        ////    logger.LogInformation("{Job} cancelled before execution", job.Name);
        ////}
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected scheduler error for {Job}", job.Name);
        }
        finally
        {
            if (acquired)
            {
                _semaphore.Release();
            }
        }
    }
}


#region DI Registration

/////*
////builder.Services.AddSingleton(new SchedulerSettings
////{
////    MaxConcurrentJobs = 2,
////    Jobs = new Dictionary<string, JobSettings>
////    {
////        ["ExampleJob"] = new()
////        {
////            Cron = "* * * * *",
////            MaxRetries = 3,
////            RetryDelaySeconds = 2
////        }
////    }
////});

////builder.Services.AddSingleton<ICronJob, ExampleJob>();

////builder.Services.AddSingleton<IJobMiddleware, JobLoggingMiddleware>();
////builder.Services.AddSingleton<IJobMiddleware, JobMetricsMiddleware>();

////builder.Services.AddSingleton<IJobExecutor, JobExecutor>();

////builder.Services.AddHostedService<SchedulerBackgroundService>();
////*/

#endregion
