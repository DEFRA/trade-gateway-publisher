using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Scheduler;

public class SchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SchedulerSettings> settings,
    ILogger<SchedulerBackgroundService> logger
) : BackgroundService
{
    private readonly SchedulerSettings _settings = settings.Value;

    private readonly TimeZoneInfo _timeZoneInfo = TimeZoneInfo.Local;

    private readonly SemaphoreSlim _semaphore = new(settings.Value.MaxConcurrentJobs);

    private List<(string JobName, CronExpression Expression)> _jobs = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using (var scope = scopeFactory.CreateScope())
        {
            var cronJobs = scope.ServiceProvider.GetRequiredService<IEnumerable<ICronJob>>();

            _jobs =
            [
                .. cronJobs
                    .Where(job => !_settings.Jobs[job.Name].Disabled)
                    .Select(job =>
                        (
                            JobName: job.Name,
                            Expression: CronExpression.Parse(_settings.Jobs[job.Name].Cron, CronFormat.IncludeSeconds)
                        )
                    ),
            ];
        }

        var nextRuns = _jobs.ToDictionary(
            x => x.JobName,
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

            foreach (var (jobName, expression) in _jobs)
            {
                var next = nextRuns[jobName];

                if (!next.HasValue || next.Value > now)
                {
                    continue;
                }

                _ = RunJobAsync(jobName, stoppingToken);

                nextRuns[jobName] = expression.GetNextOccurrence(now.AddSeconds(1), _timeZoneInfo);

                logger.LogInformation("Next run of {Job} scheduled at {NextRun}", jobName, nextRuns[jobName]);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Scheduler stopped");
    }

    private async Task RunJobAsync(string jobName, CancellationToken cancellationToken)
    {
        var acquired = false;

        try
        {
            await _semaphore.WaitAsync(cancellationToken);

            acquired = true;

            using var scope = scopeFactory.CreateScope();

            var cronJobs = scope.ServiceProvider.GetRequiredService<IEnumerable<ICronJob>>();

            var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();

            var job = cronJobs.Single(x => x.Name == jobName);

            logger.LogInformation("Running job {Job}", job.Name);

            var jobSettings = _settings.Jobs[job.Name];

            await jobExecutor.ExecuteAsync(job, jobSettings, cancellationToken);
        }
        catch (OperationCanceledException ex)
        {
            logger.LogWarning(ex, "{Job} cancelled before execution", jobName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected scheduler error for {Job}", jobName);
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
