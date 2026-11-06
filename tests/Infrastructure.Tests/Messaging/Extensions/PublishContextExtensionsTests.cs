using AwesomeAssertions;
using Defra.TradeImports.Messaging.Publishing;
using Infrastructure.Messaging.Extensions;

namespace Infrastructure.Tests.Messaging.Extensions;

public class PublishContextExtensionsTests
{
    [Fact]
    public void ToServiceBusMessage_maps_body_and_application_properties()
    {
        var publishContext = new PublishContext
        {
            MessageBody = "sb-body",
            Headers = new Dictionary<string, string> { ["h1"] = "hv1", ["h2"] = "hv2" },
        };

        var message = publishContext.ToServiceBusMessage();

        message.Body.ToString().Should().Contain("sb-body");
        message.ApplicationProperties.Should().ContainKey("h1");
        message.ApplicationProperties["h1"].Should().Be("hv1");
        message.ApplicationProperties["h2"].Should().Be("hv2");
    }
}
