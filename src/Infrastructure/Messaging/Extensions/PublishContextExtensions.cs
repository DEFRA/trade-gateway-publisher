using Azure.Messaging.ServiceBus;
using Defra.TradeImports.Messaging.Publishing;

namespace Infrastructure.Messaging.Extensions
{
    public static class PublishContextExtensions
    {
        public static ServiceBusMessage ToServiceBusMessage(this PublishContext publishContext)
        {
            var serviceBusMessage = new ServiceBusMessage(publishContext.MessageBody);

            foreach (var header in publishContext.Headers)
            {
                serviceBusMessage.ApplicationProperties.Add(header.Key, header.Value);
            }

            return serviceBusMessage;
        }
    }
}
