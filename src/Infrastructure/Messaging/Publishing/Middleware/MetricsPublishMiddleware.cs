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
        var topicName = context.GetTopicName();

        try
        {
            metrics.Start(topicName);

            await next();
        }
#pragma warning disable S2139
        catch (Exception exception)
#pragma warning restore S2139
        {
            metrics.Faulted(topicName, exception);
            throw;
        }
        finally
        {
            metrics.Complete(topicName, TimeProvider.System.GetElapsedTime(startingTimestamp).TotalMilliseconds);
        }
    }
}
