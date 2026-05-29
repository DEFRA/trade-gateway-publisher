namespace Infrastructure.Messaging
{
    public interface IMessage
    {
        string DuplicationId { get; }
    }
}
