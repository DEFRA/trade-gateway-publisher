#nullable enable

using Cronos;
using Infrastructure.Scheduler;
using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace Infrastructure.Tests.Scheduler;

public class SchedulerBackgroundServiceTests(ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task ExecuteAsync_WhenJobIsScheduled_ExecutesJob()
    {
        // Arrange
        var job = new TestCronJob("scheduled-job");

        var executor = new TestJobExecutor();

        var settings = CreateSettings(maxConcurrency: 1, ("scheduled-job", CronExpression.EverySecond.ToString()));

        var sut = CreateSut([job], executor, settings);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var success = await WaitHelper.WaitUntilAsync(
            () => executor.ExecutionCount > 0,
            timeout: TimeSpan.FromSeconds(60)
        );

        Assert.True(success);

        Assert.Contains("scheduled-job", executor.ExecutedJobs);

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleJobsExist_ExecutesAllEligibleJobs()
    {
        // Arrange
        var job1 = new TestCronJob("job-1");
        var job2 = new TestCronJob("job-2");

        var executor = new TestJobExecutor();

        var settings = CreateSettings(
            maxConcurrency: 2,
            ("job-1", CronExpression.EverySecond.ToString()),
            ("job-2", CronExpression.EverySecond.ToString())
        );

        var sut = CreateSut([job1, job2], executor, settings);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var success = await WaitHelper.WaitUntilAsync(
            () => executor.ExecutionCount >= 2,
            timeout: TimeSpan.FromSeconds(60)
        );

        Assert.True(success);

        Assert.Contains("job-1", executor.ExecutedJobs);
        Assert.Contains("job-2", executor.ExecutedJobs);

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequested_StopsScheduler()
    {
        // Arrange
        var job = new TestCronJob("cancel-job");

        var executor = new TestJobExecutor();

        var settings = CreateSettings(maxConcurrency: 1, ("cancel-job", CronExpression.EverySecond.ToString()));

        var sut = CreateSut([job], executor, settings);

        using var cts = new CancellationTokenSource();

        // Act
        await sut.StartAsync(cts.Token);

        await cts.CancelAsync();

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.True(true);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobExecutorThrows_ContinuesRunning()
    {
        // Arrange
        var loggerFactory = LoggerFactory.Create(builder =>
            builder
                .AddProvider(new XUnitLoggerProvider(outputHelper, new XUnitLoggerOptions()))
                .SetMinimumLevel(LogLevel.Trace)
        );

        var logger = loggerFactory.CreateLogger<SchedulerBackgroundService>();

        var job = new TestCronJob("failing-job");

        var executor = new ThrowingJobExecutor();

        var settings = CreateSettings(maxConcurrency: 1, ("failing-job", CronExpression.EverySecond.ToString()));

        var sut = CreateSut([job], executor, settings, logger);

        // Act
        await sut.StartAsync(CancellationToken.None);

        // Assert
        var success = await WaitHelper.WaitUntilAsync(
            () => executor.ExecutionAttempts > 0,
            timeout: TimeSpan.FromSeconds(60)
        );

        Assert.True(success);
        Assert.True(executor.ExecutionAttempts > 0);

        await sut.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_RespectsMaxConcurrency()
    {
        // Arrange
        var job1 = new TestCronJob("job-1");
        var job2 = new TestCronJob("job-2");
        var job3 = new TestCronJob("job-3");

        var executor = new ConcurrencyTrackingJobExecutor();

        var settings = CreateSettings(
            maxConcurrency: 1,
            ("job-1", CronExpression.EverySecond.ToString()),
            ("job-2", CronExpression.EverySecond.ToString()),
            ("job-3", CronExpression.EverySecond.ToString())
        );

        var sut = CreateSut([job1, job2, job3], executor, settings);

        // Act
        await sut.StartAsync(CancellationToken.None);

        await Task.Delay(3000);

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(1, executor.MaxObservedConcurrency);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobHasFutureSchedule_DoesNotRunImmediately()
    {
        // Arrange
        var job = new TestCronJob("future-job");

        var executor = new TestJobExecutor();

        var settings = CreateSettings(maxConcurrency: 1, ("future-job", "0 0 1 1 * *"));

        var sut = CreateSut([job], executor, settings);

        // Act
        await sut.StartAsync(CancellationToken.None);

        await Task.Delay(1500);

        await sut.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(0, executor.ExecutionCount);
    }

    private static SchedulerBackgroundService CreateSut(
        IEnumerable<ICronJob> jobs,
        IJobExecutor executor,
        IOptions<SchedulerSettings> settings,
        ILogger<SchedulerBackgroundService>? logger = null
    )
    {
        var services = new ServiceCollection();

        services.AddScoped<IJobExecutor>(_ => executor);

        foreach (var job in jobs)
        {
            services.AddScoped<ICronJob>(_ => job);
        }

        var provider = services.BuildServiceProvider();

        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        return new SchedulerBackgroundService(
            scopeFactory,
            settings,
            logger ?? NullLogger<SchedulerBackgroundService>.Instance
        );
    }

    private static IOptions<SchedulerSettings> CreateSettings(
        int maxConcurrency,
        params (string JobName, string Cron)[] jobs
    )
    {
        var settings = new SchedulerSettings
        {
            MaxConcurrentJobs = maxConcurrency,
            Jobs = jobs.ToDictionary(
                x => x.JobName,
                x => new JobSettings
                {
                    Cron = x.Cron,
                    MaxRetries = 0,
                    RetryDelaySeconds = 0,
                }
            ),
        };

        return Options.Create(settings);
    }

    private sealed class TestCronJob(string name) : ICronJob
    {
        public string Name => name;

        public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestJobExecutor : IJobExecutor
    {
        public int ExecutionCount { get; private set; }

        public List<string> ExecutedJobs { get; } = [];

        public Task ExecuteAsync(ICronJob job, JobSettings settings, CancellationToken cancellationToken)
        {
            ExecutionCount++;

            ExecutedJobs.Add(job.Name);

            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingJobExecutor : IJobExecutor
    {
        public int ExecutionAttempts { get; private set; }

        public Task ExecuteAsync(ICronJob job, JobSettings settings, CancellationToken cancellationToken)
        {
            ExecutionAttempts++;

            throw new InvalidOperationException("Executor failure");
        }
    }

    private sealed class ConcurrencyTrackingJobExecutor : IJobExecutor
    {
        private int _currentConcurrency;

        public int MaxObservedConcurrency { get; private set; }

        public async Task ExecuteAsync(ICronJob job, JobSettings settings, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);

            if (current > MaxObservedConcurrency)
            {
                MaxObservedConcurrency = current;
            }

            try
            {
                await Task.Delay(500, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }
    }
}
