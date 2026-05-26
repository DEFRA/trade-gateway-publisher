namespace Infrastructure.Watermark;

public interface IJobWatermarkStore
{
    Task<DateTimeOffset?> GetAsync(string jobName, CancellationToken cancellationToken = default);

    Task SetAsync(string jobName, DateTimeOffset watermark, CancellationToken cancellationToken = default);
}
