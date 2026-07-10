using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AwesomeAssertions;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Publishing;
using NSubstitute;

namespace Infrastructure.Tests.Messaging.Publishing;

public class SnsPublisherTests
{
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:test-topic.fifo";

    private readonly IAmazonSimpleNotificationService _snsClient = Substitute.For<IAmazonSimpleNotificationService>();
    private readonly SnsPublisher _sut;

    private PublishRequest? _request;

    public SnsPublisherTests()
    {
        _snsClient
            .PublishAsync(Arg.Do<PublishRequest>(request => _request = request), Arg.Any<CancellationToken>())
            .Returns(new PublishResponse());

        _sut = new SnsPublisher(_snsClient);
    }

    [Fact]
    public async Task PublishAsync_should_send_the_supplied_duplication_id_to_sns()
    {
        await _sut.PublishAsync(TopicArn, "body", duplicationId: "dedup-1");

        _request!.MessageDeduplicationId.Should().Be("dedup-1");
    }

    [Fact]
    public async Task PublishAsync_should_send_the_messages_duplication_id_to_sns()
    {
        await _sut.PublishAsync(TopicArn, new TestMessage("dedup-1"));

        _request!.MessageDeduplicationId.Should().Be("dedup-1");
    }

    private sealed record TestMessage(string DuplicationId) : IMessage;
}
