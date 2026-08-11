namespace Infrastructure.Messaging.Publishing
{
    public class PublishContext
    {
        public string QueueName { get; init; } = default!;

        public string MessageBody { get; init; } = default!;

        public string? Subject { get; init; }

        public Dictionary<string, string> Headers { get; init; } = new();
    }
}
