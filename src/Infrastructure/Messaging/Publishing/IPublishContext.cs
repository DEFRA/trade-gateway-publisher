namespace Infrastructure.Messaging.Publishing;

public interface IPublishContext
{
    string GetTopicName();
    void SetTraceId(string? traceId);
}
