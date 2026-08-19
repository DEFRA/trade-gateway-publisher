using AwesomeAssertions;

using Infrastructure.Messaging.Extensions;
using Infrastructure.Messaging.Publishing;

namespace Infrastructure.Tests.Messaging.Extensions;

public class PublishContextExtensionsTests
{
    private const string TopicArn = "arn:aws:sns:eu-west-2:000000000000:test-topic.fifo";

    [Fact]
    public void ToSnsPublishRequest_maps_message_subject_topic_and_headers_when_subject_present()
    {
        var publishContext = new PublishContext
        {
            MessageBody = "the-body",
            Subject = "my-subject",
            Headers = new Dictionary<string, string>
            {
                ["k1"] = "v1",
            }
        };

        var request = publishContext.ToSnsPublishRequest(TopicArn);

        request.TopicArn.Should().Be(TopicArn);
        request.Message.Should().Be("the-body");
        request.Subject.Should().Be("my-subject");
        request.MessageGroupId.Should().Be("my-subject");

        request.MessageAttributes.Should().ContainKey("k1");
        var attr = request.MessageAttributes["k1"];
        attr.DataType.Should().Be("String");
        attr.StringValue.Should().Be("v1");
    }

    [Fact]
    public void ToSnsPublishRequest_generates_message_group_id_when_subject_missing()
    {
        var publishContext = new PublishContext
        {
            MessageBody = "body-without-subject",
            Subject = null,
            Headers = new Dictionary<string, string>()
        };

        var request = publishContext.ToSnsPublishRequest(TopicArn);

        request.Subject.Should().BeNull();
        request.MessageGroupId.Should().NotBeNullOrEmpty();
        request.MessageGroupId.Length.Should().Be(32); // Guid.ToString("N") => 32 hex chars
    }

    [Fact]
    public void ToServiceBusMessage_maps_body_and_application_properties()
    {
        var publishContext = new PublishContext
        {
            MessageBody = "sb-body",
            Headers = new Dictionary<string, string>
            {
                ["h1"] = "hv1",
                ["h2"] = "hv2",
            }
        };

        var message = publishContext.ToServiceBusMessage();

        message.Body.ToString().Should().Contain("sb-body");
        message.ApplicationProperties.Should().ContainKey("h1");
        message.ApplicationProperties["h1"].Should().Be("hv1");
        message.ApplicationProperties["h2"].Should().Be("hv2");
    }

    [Fact]
    public void SetTraceId_sets_trace_header_when_value_present()
    {
        var publishContext = new PublishContext
        {
            MessageBody = "b",
            Headers = new Dictionary<string, string>()
        };

        publishContext.SetTraceId("trace-123");

        publishContext.Headers.Should().ContainKey(MetricNames.TraceKey);
        publishContext.Headers[MetricNames.TraceKey].Should().Be("trace-123");
    }

    [Fact]
    public void SetTraceId_does_not_set_when_value_null_or_empty()
    {
        var publishContext = new PublishContext
        {
            MessageBody = "b",
            Headers = new Dictionary<string, string>()
        };

        publishContext.SetTraceId(null);
        publishContext.SetTraceId(string.Empty);

        publishContext.Headers.Should().NotContainKey(MetricNames.TraceKey);
    }
}
