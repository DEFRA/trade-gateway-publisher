namespace CronJobs.Leasing;

public interface IJobLeaseProvider
{
    Task<IAsyncDisposable?> TryAcquireAsync(string leaseName, TimeSpan duration, CancellationToken cancellationToken);
}
