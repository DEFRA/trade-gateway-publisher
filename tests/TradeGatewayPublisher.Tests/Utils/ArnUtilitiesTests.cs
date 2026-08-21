using System;
using TradeGatewayPublisher.Utils;
using Xunit;

namespace TradeGatewayPublisher.Tests.Utils;

public class ArnUtilitiesTests
{
    [Fact]
    public void GetTopicName_returns_last_segment_for_valid_arn()
    {
        var arn = "arn:aws:sns:eu-west-2:123456789012:my-topic.fifo";

        var name = ArnUtilities.GetTopicName(arn);

        Assert.Equal("my-topic.fifo", name);
    }

    [Fact]
    public void GetTopicName_throws_FormatException_for_invalid_arn()
    {
        var arn = "invalid:arn:short";

        Assert.Throws<FormatException>(() => ArnUtilities.GetTopicName(arn));
    }
}
