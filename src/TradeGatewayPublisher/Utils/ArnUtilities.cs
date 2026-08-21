namespace TradeGatewayPublisher.Utils
{
    public static class ArnUtilities
    {
        public static string GetTopicName(string topicArn)
        {
            var parts = topicArn.Split(':');
            return parts.Length < 6 ? throw new FormatException("Invalid SNS Topic ARN format.") : parts[^1];
        }
    }
}
