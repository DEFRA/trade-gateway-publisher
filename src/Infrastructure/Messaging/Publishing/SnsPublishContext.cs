using Amazon.SimpleNotificationService.Model;

namespace Infrastructure.Messaging.Publishing
{
    public class SnsPublishContext : IPublishContext
    {
        public string TopicArn { get; set; } = default!;

        public string MessageBody { get; set; } = default!;

        public string? Subject { get; set; }

        public Dictionary<string, MessageAttributeValue> Headers { get; } = new();

        public string GetTopicName()
        {
            var parts = TopicArn.Split(':');
            return parts.Length < 6 ? throw new FormatException("Invalid SNS Topic ARN format.") : parts[^1];
        }

        public PublishRequest ToPublishRequest()
        {
            var request = new PublishRequest
            {
                TopicArn = TopicArn,
                Message = MessageBody,
                Subject = Subject,
                MessageGroupId = Subject ?? Guid.CreateVersion7().ToString("N"),
                MessageAttributes = [],
            };

            foreach (var header in Headers)
            {
                request.MessageAttributes[header.Key] = header.Value;
            }

            return request;
        }

        public void SetTraceId(string? traceId)
        {
            if (!string.IsNullOrEmpty(traceId))
            {
                Headers[MetricNames.TraceKey] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = traceId,
                };
            }
        }
    }
}
