using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Azure;

namespace Infrastructure.Messaging.Publishing;

public class AsbPublisher(IAzureClientFactory<ServiceBusSender> serviceBusSenderFactory) : IAsbPublisher
{
    public async Task PublishAsync(
        string queueName,
        string messageId,
        Dictionary<string, string> messageHeaders,
        string messageBody,
        CancellationToken cancellationToken = default
    )
    {
        var sender = serviceBusSenderFactory.CreateClient(queueName);

        var serviceBusMessage = new ServiceBusMessage(messageBody) { MessageId = messageId };

        foreach (var header in messageHeaders)
        {
            serviceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
        }

        await sender.SendMessageAsync(serviceBusMessage, cancellationToken);

        await sender.DisposeAsync();
    }
}
