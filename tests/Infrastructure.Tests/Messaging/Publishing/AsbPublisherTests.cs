using Azure.Messaging.ServiceBus;
using Infrastructure.Messaging.Publishing;
using Microsoft.Extensions.Azure;
using NSubstitute;

namespace Infrastructure.Tests.Messaging.Publishing;

public class AsbPublisherTests
{
    [Fact]
    public async Task PublishAsync_throws_when_topic_name_missing()
    {
        var factory = Substitute.For<IAzureClientFactory<ServiceBusSender>>();
        var sut = new AsbPublisher(factory);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.PublishAsync(string.Empty, "id", new Dictionary<string, string>(), "body")
        );
        Assert.Equal("topicName", ex.ParamName);
    }

    [Fact]
    public async Task PublishAsync_throws_when_message_body_missing()
    {
        var factory = Substitute.For<IAzureClientFactory<ServiceBusSender>>();
        var sut = new AsbPublisher(factory);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.PublishAsync("topic", "id", new Dictionary<string, string>(), string.Empty)
        );
        Assert.Equal("messageBody", ex.ParamName);
    }

    [Fact]
    public async Task PublishAsync_sends_message_with_message_id_and_headers()
    {
        var factory = Substitute.For<IAzureClientFactory<ServiceBusSender>>();
        var sender = Substitute.For<ServiceBusSender>();
        factory.CreateClient("topic1").Returns(sender);

        ServiceBusMessage? sent = null;
        sender
            .SendMessageAsync(Arg.Do<ServiceBusMessage>(m => sent = m), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var sut = new AsbPublisher(factory);

        var headers = new Dictionary<string, string> { ["h1"] = "v1" };

        await sut.PublishAsync("topic1", "msg-1", headers, "hello-body");

        Assert.NotNull(sent);
        Assert.Equal("msg-1", sent!.MessageId);
        Assert.True(sent.ApplicationProperties.ContainsKey("h1"));
        Assert.Equal("v1", sent.ApplicationProperties["h1"]);
        Assert.Contains("hello-body", sent.Body.ToString());
    }

    [Fact]
    public async Task PublishAsync_runs_middlewares_and_pipeline_applies_headers_from_middlewares()
    {
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

        var sut = new AsbPublisher(factory, [mw1, mw2]);

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
