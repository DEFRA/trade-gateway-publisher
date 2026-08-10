using Amazon.SimpleNotificationService.Model;
using Azure.Messaging.ServiceBus;

namespace Infrastructure.Messaging.Publishing
{
    public class AsbPublishContext : IPublishContext
    {
        public string QueueName { get; set; } = default!;

        public string MessageBody { get; set; } = default!;

        public string? Subject { get; set; }

        public Dictionary<string, string> Headers { get; init; } = new();

        public string GetTopicName() => QueueName;

        public ServiceBusMessage ToServiceBusMessage()
        {
            var serviceBusMessage = new ServiceBusMessage(MessageBody);

            foreach (var header in Headers)
            {
                serviceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
            }

            return serviceBusMessage;
        }

        public void SetTraceId(string? traceId)
        {
            if (!string.IsNullOrEmpty(traceId))
            {
                Headers[MetricNames.TraceKey] = traceId;
            }
        }
    }
}
