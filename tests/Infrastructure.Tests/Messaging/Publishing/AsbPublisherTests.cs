using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Azure;
using Microsoft.FeatureManagement;
using NSubstitute;

namespace Infrastructure.Tests.Messaging.Publishing;

public class AsbPublisherTests
{
    private readonly AsbPublisher _sut;
    private readonly IAzureClientFactory<ServiceBusSender> _factory;
    private readonly IFeatureManager _featureManager;

    private const string AzureServiceBusFeatureName = "AzureServiceBusPublishing";

    public AsbPublisherTests()
    {
        _factory = Substitute.For<IAzureClientFactory<ServiceBusSender>>();
        _featureManager = Substitute.For<IFeatureManager>();
        _sut = new AsbPublisher(_factory, _featureManager);
    }

    [Fact]
    public async Task PublishAsync_throws_when_topic_name_missing()
    {
        _featureManager.IsEnabledAsync(AzureServiceBusFeatureName).Returns(Task.FromResult(true));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.PublishAsync(string.Empty, "id", new Dictionary<string, string>(), "body")
        );
        Assert.Equal("topicName", ex.ParamName);
    }

    [Fact]
    public async Task PublishAsync_throws_when_message_body_missing()
    {
        _featureManager.IsEnabledAsync(AzureServiceBusFeatureName).Returns(Task.FromResult(true));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.PublishAsync("topic", "id", new Dictionary<string, string>(), string.Empty)
        );
        Assert.Equal("messageBody", ex.ParamName);
    }

    [Fact]
    public async Task PublishAsync_does_nothing_when_feature_disabled()
    {
        _featureManager.IsEnabledAsync(AzureServiceBusFeatureName).Returns(Task.FromResult(false));

        var sender = Substitute.For<ServiceBusSender>();

        _factory.CreateClient("topic-should-not-be-called").Returns(sender);

        await _sut.PublishAsync("topic-should-not-be-called", "id", new Dictionary<string, string>(), "body");

        // _factory.CreateClient should not have been called
        _factory.DidNotReceive().CreateClient(Arg.Any<string>());
    }

    [Fact]
    public async Task PublishAsync_sends_message_with_message_id_and_headers()
    {
        _featureManager.IsEnabledAsync(AzureServiceBusFeatureName).Returns(Task.FromResult(true));

        var sender = Substitute.For<ServiceBusSender>();
        _factory.CreateClient("topic1").Returns(sender);

        ServiceBusMessage? sent = null;
        sender
            .SendMessageAsync(Arg.Do<ServiceBusMessage>(m => sent = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var headers = new Dictionary<string, string> { ["h1"] = "v1" };

        await _sut.PublishAsync("topic1", "msg-1", headers, "hello-body");

        Assert.NotNull(sent);
        Assert.Equal("msg-1", sent!.MessageId);
        Assert.True(sent.ApplicationProperties.ContainsKey("h1"));
        Assert.Equal("v1", sent.ApplicationProperties["h1"]);
        Assert.Contains("hello-body", sent.Body.ToString());
    }

    /// <summary>
    /// Test the middleware and pipeline sequences. NOTE: As this impacts the SUT constructor it builds its own SUT
    /// </summary>
    [Fact]
    public async Task PublishAsync_runs_middlewares_and_pipeline_applies_headers_from_middlewares()
    {
        var featureManager = Substitute.For<IFeatureManager>();
        featureManager.IsEnabledAsync(AzureServiceBusFeatureName).Returns(Task.FromResult(true));

        var factory = Substitute.For<IAzureClientFactory<ServiceBusSender>>();
        var sender = Substitute.For<ServiceBusSender>();
        factory.CreateClient("topic-x").Returns(sender);

        ServiceBusMessage? sent = null;
        sender
            .SendMessageAsync(Arg.Do<ServiceBusMessage>(m => sent = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var mw1 = Substitute.For<IPublishMiddleware>();
        mw1.InvokeAsync(Arg.Any<PublishContext>(), Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ctx = ci.Arg<PublishContext>();
                ctx.Headers["from-mw1"] = "1";
                var next = ci.Arg<Func<Task>>();
                await next();
                ctx.Headers["mw1-after"] = "done";
            });

        var mw2 = Substitute.For<IPublishMiddleware>();
        mw2.InvokeAsync(Arg.Any<PublishContext>(), Arg.Any<Func<Task>>(), Arg.Any<CancellationToken>())
            .Returns(async ci =>
            {
                var ctx = ci.Arg<PublishContext>();
                ctx.Headers["from-mw2"] = "2";
                var next = ci.Arg<Func<Task>>();
                await next();
                ctx.Headers["mw2-after"] = "done";
            });

        var sut = new AsbPublisher(factory, featureManager, [mw1, mw2]);

        var headers = new Dictionary<string, string>();

        await sut.PublishAsync("topic-x", "id-42", headers, "payload");

        Assert.NotNull(sent);
        Assert.Equal("id-42", sent!.MessageId);
        Assert.True(sent.ApplicationProperties.ContainsKey("from-mw1"));
        Assert.True(sent.ApplicationProperties.ContainsKey("from-mw2"));
        Assert.Equal("1", sent.ApplicationProperties["from-mw1"]);
        Assert.Equal("2", sent.ApplicationProperties["from-mw2"]);

        // middlewares that set after-next headers will have modified the original headers dictionary after send
        Assert.Equal("done", headers["mw1-after"]);
        Assert.Equal("done", headers["mw2-after"]);
    }
}
