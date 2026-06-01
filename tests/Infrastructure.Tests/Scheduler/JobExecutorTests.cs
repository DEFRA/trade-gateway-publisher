#nullable enable
using Defra.TradeImports.Tracing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Logging.Abstractions;

namespace Infrastructure.Tests.Scheduler;

public class JobExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_WhenJobSucceeds_ExecutesOnce()
    {
        // Arrange
        var job = new TestCronJob();

        var settings = new JobSettings { MaxRetries = 3, RetryDelaySeconds = 1 };

        var sut = new JobExecutor([], NullLogger<JobExecutor>.Instance, new TraceContextAccessor());

        // Act
        await sut.ExecuteAsync(job, settings, CancellationToken.None);

        // Assert
        Assert.Equal(1, job.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobFailsAndRetriesEventuallySucceeds_RetriesUntilSuccess()
    {
        // Arrange
        var job = new TestCronJob { FailuresBeforeSuccess = 2 };

        var settings = new JobSettings { MaxRetries = 3, RetryDelaySeconds = 0 };

        var sut = new JobExecutor([], NullLogger<JobExecutor>.Instance, new TraceContextAccessor());

        // Act
        await sut.ExecuteAsync(job, settings, CancellationToken.None);

        // Assert
        Assert.Equal(3, job.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobFailsBeyondRetryLimit_ThrowsException()
    {
        // Arrange
        var job = new TestCronJob { AlwaysFail = true };

        var settings = new JobSettings { MaxRetries = 2, RetryDelaySeconds = 0 };

        var sut = new JobExecutor([], NullLogger<JobExecutor>.Instance, new TraceContextAccessor());

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(job, settings, CancellationToken.None)
        );

        Assert.Equal("Job failure", exception.Message);

        // initial attempt + 2 retries
        Assert.Equal(3, job.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequestedBeforeExecution_ThrowsOperationCanceledException()
    {
        // Arrange
        var job = new TestCronJob();

        var settings = new JobSettings { MaxRetries = 1, RetryDelaySeconds = 0 };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var sut = new JobExecutor([], NullLogger<JobExecutor>.Instance, new TraceContextAccessor());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(job, settings, cts.Token));

        Assert.Equal(0, job.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenJobThrowsOperationCanceledException_PropagatesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        var job = new CancellingCronJob(cts);

        var settings = new JobSettings { MaxRetries = 3, RetryDelaySeconds = 0 };

        var sut = new JobExecutor([], NullLogger<JobExecutor>.Instance, new TraceContextAccessor());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(job, settings, cts.Token));

        Assert.Equal(1, job.ExecutionCount);
    }

    [Fact]
    public async Task ExecuteAsync_ExecutesMiddlewaresInCorrectOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var middleware1 = new TestMiddleware("middleware-1", executionOrder);

        var middleware2 = new TestMiddleware("middleware-2", executionOrder);

        var job = new OrderedCronJob(executionOrder);

        var settings = new JobSettings { MaxRetries = 0, RetryDelaySeconds = 0 };

        var sut = new JobExecutor(
            [middleware1, middleware2],
            NullLogger<JobExecutor>.Instance,
            new TraceContextAccessor()
        );

        // Act
        await sut.ExecuteAsync(job, settings, CancellationToken.None);

        // Assert
        Assert.Equal(
            ["middleware-1-before", "middleware-2-before", "job", "middleware-2-after", "middleware-1-after"],
            executionOrder
        );
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaxRetriesIsNegative_TreatsAsZero()
    {
        // Arrange
        var job = new TestCronJob { AlwaysFail = true };

        var settings = new JobSettings { MaxRetries = -10, RetryDelaySeconds = 0 };

        var sut = new JobExecutor([], NullLogger<JobExecutor>.Instance, new TraceContextAccessor());

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(job, settings, CancellationToken.None)
        );

        Assert.Equal(1, job.ExecutionCount);
    }

    private sealed class TestCronJob : ICronJob
    {
        public string Name => "test-job";

        public int ExecutionCount { get; private set; }

        public int FailuresBeforeSuccess { get; set; }

        public bool AlwaysFail { get; set; }

        public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
        {
            ExecutionCount++;

            if (AlwaysFail)
            {
                throw new InvalidOperationException("Job failure");
            }

            if (ExecutionCount <= FailuresBeforeSuccess)
            {
                throw new InvalidOperationException("Job failure");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CancellingCronJob(CancellationTokenSource cts) : ICronJob
    {
        public string Name => "cancel-job";

        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
        {
            ExecutionCount++;

            cts.Cancel();

            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    private sealed class OrderedCronJob(List<string> executionOrder) : ICronJob
    {
        public string Name => "ordered-job";

        public Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
        {
            executionOrder.Add("job");

            return Task.CompletedTask;
        }
    }

    private sealed class TestMiddleware(string name, List<string> executionOrder) : IJobMiddleware
    {
        public async Task InvokeAsync(
            JobContext context,
            CancellationToken cancellationToken,
            JobExecutionDelegate next
        )
        {
            executionOrder.Add($"{name}-before");

            await next(context, cancellationToken);

            executionOrder.Add($"{name}-after");
        }
    }
}
