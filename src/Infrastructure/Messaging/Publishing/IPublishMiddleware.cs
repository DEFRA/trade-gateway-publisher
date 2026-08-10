namespace Infrastructure.Messaging.Publishing;

public interface IPublishMiddleware
{
    Task InvokeAsync(IPublishContext context, Func<Task> next, CancellationToken cancellationToken = default);
}
