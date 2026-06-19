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
        traceContextAccessor.Context = new TraceContext { TraceId = Guid.CreateVersion7().ToString("N") };

        var context = new JobContext(Guid.CreateVersion7().ToString(), job.Name);
        cancellationToken.ThrowIfCancellationRequested();
        await ExecutePipelineAsync(job, context, cancellationToken);

        logger.LogInformation("{Job} completed successfully", job.Name);
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
}
