using Amazon.SimpleNotificationService.Model;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Publishing;

namespace Infrastructure.Messaging.Extensions
{
    public static class PublishContextExtensions
    {
        public static PublishRequest ToSnsPublishRequest(this PublishContext publishContext, string topicArn)
        {
            var request = new PublishRequest
            {
                TopicArn = topicArn,
                Message = publishContext.MessageBody,
                Subject = publishContext.Subject,
                MessageGroupId = publishContext.Subject ?? Guid.CreateVersion7().ToString("N"),
                MessageAttributes = [],
            };

            foreach (var header in publishContext.Headers)
            {
                request.MessageAttributes[header.Key] = new MessageAttributeValue
                {
                    DataType = "String",
                    StringValue = header.Value,
                };
            }

            return request;
        }

        public static ServiceBusMessage ToServiceBusMessage(this PublishContext publishContext)
        {
            var serviceBusMessage = new ServiceBusMessage(publishContext.MessageBody);

            foreach (var header in publishContext.Headers)
            {
                serviceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
            }

            return serviceBusMessage;
        }

        public static void SetTraceId(this PublishContext publishContext, string? traceId)
        {
            if (!string.IsNullOrEmpty(traceId))
            {
                publishContext.Headers[MetricNames.TraceKey] = traceId;
            }
        }
    }
}
