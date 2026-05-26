using Infrastructure.Scheduler;
using Infrastructure.Watermark;

namespace TradeGatewayPublisher.Jobs.Middleware;

public sealed class WatermarkMiddleware(IJobWatermarkStore watermarkStore) : IJobMiddleware
{
    public async Task InvokeAsync(JobContext context, CancellationToken cancellationToken, JobExecutionDelegate next)
    {
        var watermark =
            await watermarkStore.GetAsync(context.Name, cancellationToken) ?? DateTimeOffset.UtcNow.AddMinutes(-5);
        context.Set(new WatermarkContext(watermark, DateTimeOffset.UtcNow));
        await next(context, cancellationToken);
        await watermarkStore.SetAsync(context.Name, DateTimeOffset.UtcNow, cancellationToken);
    }
}
