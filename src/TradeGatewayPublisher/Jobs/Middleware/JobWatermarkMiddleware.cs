using Infrastructure.Scheduler;
using Infrastructure.Watermark;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class JobWatermarkMiddleware(IJobWatermarkStore watermarkStore) : IJobMiddleware
{
    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        var watermark =
            await watermarkStore.GetAsync(context.Name, cancellationToken) ?? DateTimeOffset.UtcNow.AddMinutes(-5);
        var watermarkContext = new WatermarkContext(watermark, DateTimeOffset.UtcNow);
        context.Set(watermarkContext);
        await next(context, cancellationToken);
        await watermarkStore.SetAsync(context.Name, watermarkContext.Now, cancellationToken);
    }
}
