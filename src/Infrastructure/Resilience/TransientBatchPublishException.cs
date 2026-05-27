using Amazon.SimpleNotificationService.Model;

namespace Infrastructure.Resilience;

public class TransientBatchPublishException(List<BatchResultErrorEntry> failures) : Exception
{
    public IReadOnlyCollection<string> FailedEntryIds { get; } = failures.Select(f => f.Id).ToArray();

    public IReadOnlyCollection<string> FailureCodes { get; } = failures.Select(f => f.Code ?? "Unknown").ToArray();
}
