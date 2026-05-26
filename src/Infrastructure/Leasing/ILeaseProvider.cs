namespace Infrastructure.Leasing;

public interface ILeaseProvider
{
    Task<IAsyncDisposable?> TryAcquireAsync(string leaseName, TimeSpan duration, CancellationToken cancellationToken);
}
