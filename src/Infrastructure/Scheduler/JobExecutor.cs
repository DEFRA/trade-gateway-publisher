using Defra.TradeImports.Tracing;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Scheduler;

public sealed class JobExecutor(
    IEnumerable<IJobMiddleware> middlewares,
    ILogger<JobExecutor> logger,
    ITraceContextAccessor traceContextAccessor
) : IJobExecutor
{
    private readonly IReadOnlyList<IJobMiddleware> _middlewares = middlewares.ToList();

    public async Task ExecuteAsync(ICronJob job, JobSettings settings, CancellationToken cancellationToken)
    {
        traceContextAccessor.Context = new TraceContext() { TraceId = Guid.CreateVersion7().ToString("N") };
        var context = new JobContext(Guid.CreateVersion7().ToString(), job.Name);
        var maxRetries = Math.Max(0, settings.MaxRetries);

        for (var attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await ExecutePipelineAsync(job, context, cancellationToken);

                logger.LogInformation("{Job} completed successfully on attempt {Attempt}", job.Name, attempt);

                return;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation(ex, "{Job} cancelled during execution", job.Name);

                    throw;
                }

                var isLastAttempt = attempt > maxRetries;

                if (isLastAttempt)
                {
                    logger.LogError(ex, "{Job} failed after {Attempts} attempts", job.Name, attempt);

                    throw;
                }

                var delay = GetBackoffDelay(attempt, settings);

                logger.LogWarning(
                    ex,
                    "{Job} failed on attempt {Attempt}. Retrying in {Delay}",
                    job.Name,
                    attempt,
                    delay
                );

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private Task ExecutePipelineAsync(ICronJob job, JobContext context, CancellationToken cancellationToken)
    {
        JobExecutionDelegate pipeline = job.ExecuteAsync;

        foreach (var middleware in _middlewares.Reverse())
        {
            var next = pipeline;

            pipeline = (_, ct) => middleware.InvokeAsync(context, ct, next);
        }

        return pipeline(context, cancellationToken);
    }

    private static TimeSpan GetBackoffDelay(int attempt, JobSettings settings)
    {
        var baseSeconds = Math.Max(1, settings.RetryDelaySeconds);

        var exponent = Math.Min(attempt - 1, 10);

        var seconds = baseSeconds * (int)Math.Pow(2, exponent);

        var jitterMs = Random.Shared.Next(100, 500);

        var delay = TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(jitterMs);

        var maxDelay = TimeSpan.FromMinutes(2);

        return delay <= maxDelay ? delay : maxDelay;
    }
}
