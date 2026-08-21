using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Infrastructure.Tests.Messaging.Publishing;

public class NullAsbPublisherTests
{
    private readonly NullAsbPublisher _sut;
    private readonly ILogger<NullAsbPublisher> _logger;

    public NullAsbPublisherTests()
    {
        _logger = Substitute.For<ILogger<NullAsbPublisher>>();
        _sut = new NullAsbPublisher(_logger);
    }

    [Fact]
    public async Task PublishAsync_logs_that_publishing_is_disabled()
    {
        await _sut.PublishAsync("topic-should-not-be-called", "id", new Dictionary<string, string>(), "body");

        _logger.Received().LogInformation("Publishing to Azure Service Bus is disabled");
    }
}
