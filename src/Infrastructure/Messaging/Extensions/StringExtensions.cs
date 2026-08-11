namespace Infrastructure.Messaging.Extensions
{
    public static class StringExtensions
    {
        public static string ToQueueNameFromTopicArn(this string topicArn)
        {
            var parts = topicArn.Split(':');
            return parts.Length < 6 ? throw new FormatException("Invalid SNS Topic ARN format.") : parts[^1];
        }
    }
}
