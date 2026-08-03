using Amazon.SQS.Model;

namespace Infrastructure.Messaging.Consuming;

public class MessageContext
{
    public required Message Message { get; init; }

    public required string QueueUrl { get; init; }
    public required Type ConsumerType { get; init; }

    public string Body => Message.Body;

    public string MessageId => Message.MessageId;

    public IReadOnlyDictionary<string, MessageAttributeValue> Headers =>
        (Message.MessageAttributes ?? new Dictionary<string, MessageAttributeValue>()).AsReadOnly();

    public string? GetHeader(string key)
    {
        return Headers != null && Headers.TryGetValue(key, out var value) ? value.StringValue : null;
    }

    public string GetTraceId()
    {
        return GetHeader(MetricNames.TraceKey) ?? Guid.NewGuid().ToString("N");
    }
}
