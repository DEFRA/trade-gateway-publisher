namespace Data;

public interface IDbTransaction : IDisposable
{
    Task Commit(CancellationToken cancellationToken);
}
