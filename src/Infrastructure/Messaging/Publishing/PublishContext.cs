using Amazon.SimpleNotificationService.Model;

namespace Infrastructure.Messaging.Publishing
{
    public class PublishContext
    {
        public string TopicArn { get; set; } = default!;

        public string MessageBody { get; set; } = default!;

        public string? Subject { get; set; }

        public Dictionary<string, MessageAttributeValue> Headers { get; } = new();

        public PublishRequest ToPublishRequest()
        {
            var request = new PublishRequest
            {
                TopicArn = TopicArn,
                Message = MessageBody,
                Subject = Subject,
                MessageGroupId = Subject ?? Guid.CreateVersion7().ToString("N"),
            };

            foreach (var header in Headers)
            {
                request.MessageAttributes[header.Key] = header.Value;
            }

            return request;
        }
    }
}
