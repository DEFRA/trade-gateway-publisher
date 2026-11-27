using Azure.Messaging.ServiceBus;

namespace Infrastructure.Messaging.Extensions;

public static class ServiceBusMessageExtensions
{
    public static ServiceBusMessage WithMessageTtl(this ServiceBusMessage serviceBusMessage, double? messageTtl)
    {
        if (messageTtl.HasValue)
        {
            serviceBusMessage.TimeToLive = TimeSpan.FromSeconds(messageTtl.Value);
        }

        return serviceBusMessage;
    }
}
