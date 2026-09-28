using Infrastructure.Messaging;
using Xunit;

namespace Infrastructure.Tests.Messaging;

public class EntraOptionsTests
{
    [Fact]
    public void EntraOptions_has_required_web_identity_properties()
    {
        var opts = new EntraOptions
        {
            Namespace = "ns.servicebus.windows.net",
            TenantId = "t",
            ClientId = "c",
            Scope = "s",
            Audience = "aud-value",
            SigningAlgorithm = "RS256",
        };

        Assert.Equal("aud-value", opts.Audience);
        Assert.Equal("RS256", opts.SigningAlgorithm);
    }

    [Fact]
    public void TracesServiceBusOptions_can_contain_entra_options()
    {
        var traces = new TracesServiceBusOptions
        {
            ConnectionString = null,
            Ched = new ServiceBusTopic { TopicName = "ched" },
            Intra = new ServiceBusTopic { TopicName = "intra" },
            EntraOptions = new EntraOptions
            {
                Namespace = "ns.servicebus.windows.net",
                TenantId = "t",
                ClientId = "c",
                Scope = "s",
                Audience = "aud",
                SigningAlgorithm = "RS256",
            },
        };

        Assert.NotNull(traces.EntraOptions);
        Assert.Equal("aud", traces.EntraOptions!.Audience);
    }
}
