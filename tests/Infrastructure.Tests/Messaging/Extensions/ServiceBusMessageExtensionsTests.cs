using AwesomeAssertions;
using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Extensions;

namespace Infrastructure.Tests.Messaging.Extensions;

public class ServiceBusMessageExtensionsTests
{
    [Fact]
    public void WithMessageTtl_sets_TimeToLive_on_the_message()
    {
        var message = new ServiceBusMessage();

        message.WithMessageTtl(1);

        message.TimeToLive.Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void With_Null_MessageTtl_then_message_has_default_ttl()
    {
        var message = new ServiceBusMessage();

        message.WithMessageTtl(null);

        message.TimeToLive.Should().Be(TimeSpan.MaxValue);
    }
}
