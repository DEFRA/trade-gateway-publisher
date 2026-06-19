using System.Text.Json;
using Infrastructure.Scheduler;
using Refit;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class JobRetryMiddleware(ILogger<JobRetryMiddleware> logger, JobSettings settings) : IJobMiddleware
{
    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        var maxRetries = Math.Max(0, settings.MaxRetries);

        for (var attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await next(context, cancellationToken);

                logger.LogInformation("{Job} completed successfully on attempt {Attempt}", context.Name, attempt);

                return;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    logger.LogInformation(ex, "{Job} cancelled during execution", context.Name);
                    throw;
                }

                var isLastAttempt = attempt > maxRetries;

                if (isLastAttempt)
                {
                    if (ex is ValidationApiException validationEx)
                    {
                        logger.LogWarning(
                            validationEx,
                            "{Job} failed validation - {Data}",
                            context.Name,
                            JsonSerializer.Serialize(validationEx.Content)
                        );
                    }

                    logger.LogError(ex, "{Job} failed after {Attempts} attempts", context.Name, attempt);
                    throw;
                }

                var delay = GetBackoffDelay(attempt, settings);

                logger.LogWarning(
                    ex,
                    "{Job} failed on attempt {Attempt}. Retrying in {Delay}",
                    context.Name,
                    attempt,
                    delay
                );

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static TimeSpan GetBackoffDelay(int attempt, JobSettings settings)
    {
        var baseSeconds = Math.Max(1, settings.RetryDelaySeconds);
        var exponent = Math.Min(attempt - 1, 10);

        var seconds = baseSeconds * (int)Math.Pow(2, exponent);
        var jitterMs = Random.Shared.Next(100, 500);

        var delay = TimeSpan.FromSeconds(seconds) + TimeSpan.FromMilliseconds(jitterMs);

        return delay <= TimeSpan.FromMinutes(2) ? delay : TimeSpan.FromMinutes(2);
    }
}
