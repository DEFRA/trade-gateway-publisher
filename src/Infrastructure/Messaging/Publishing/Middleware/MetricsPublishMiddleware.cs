namespace Infrastructure.Messaging.Publishing.Middleware;

public class MetricsPublishMiddleware(PublishMetrics metrics) : IPublishMiddleware
{
    public async Task InvokeAsync(
        PublishContext context,
        Func<Task> next,
        CancellationToken cancellationToken = default
    )
    {
        var startingTimestamp = TimeProvider.System.GetTimestamp();

        try
        {
            metrics.Start(context.QueueName);

            await next();
        }
#pragma warning disable S2139
        catch (Exception exception)
#pragma warning restore S2139
        {
            metrics.Faulted(context.QueueName, exception);
            throw;
        }
        finally
        {
            metrics.Complete(
                context.QueueName,
                TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds
            );
        }
    }
}
