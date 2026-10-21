using System.Text.Json;
using Infrastructure;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Options;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using TradeGatewayPublisher.Config;

namespace TradeGatewayPublisher.Features.ClassificationTreeNodeChanges;

public sealed partial class TracesClassificationTreeNodeChangesJob(
    IReferenceDataClient referenceDataClient,
    ISnsPublisher snsPublisher,
    IOptions<TracesUpdatePublisherOptions> options,
    ILogger<TracesClassificationTreeNodeChangesJob> logger
) : ICronJob
{
    public string Name => "TracesClassificationTreeNodeChangesJob";

    private static string SourceTag => "ClassificationTreeNode";

    private string GetTopicArn() => options.Value.ClassificationTreeNodeInternalTopicArn;

    public async Task ExecuteAsync(JobContext context, CancellationToken cancellationToken)
    {
        var watermark = context.GetRequired<WatermarkContext>();
        var changesFoundCount = 0;

        foreach (var treeId in options.Value.ClassificationTreeIds)
        {
            try
            {
                var updatesResponse = await referenceDataClient.FindCommodityTreeUpdates(
                    treeId,
                    watermark.Watermark,
                    watermark.Now,
                    cancellationToken
                );

                await updatesResponse.EnsureSuccessfulAsync();

                var topicArn = GetTopicArn();

                foreach (var update in updatesResponse.Content?.Nodes ?? [])
                {
                    var duplicationId = update.GetDuplicationId(
                        updatesResponse?.Content?.RetrievedAt ?? DateTimeOffset.Now
                    );

                    // Publish each update to SNS - this could prob become a batch
                    await snsPublisher.PublishAsync(
                        topicArn,
                        update.ToJson(),
                        headers: new Dictionary<string, string>()
                        {
                            { ClassificationTreeMessageHeaderKeys.TreeId, treeId },
                            { ClassificationTreeMessageHeaderKeys.DuplicationId, duplicationId },
                        },
                        cancellationToken: cancellationToken,
                        duplicationId: duplicationId
                    );

                    LogPublishedTreeNode(SourceTag, duplicationId, topicArn);

                    changesFoundCount++;
                }
            }
#pragma warning disable S2139
            catch (ValidationApiException e)
#pragma warning restore S2139
            {
                LogJobFailedValidation(Name, JsonSerializer.Serialize(e.Content));
                throw;
            }
        }

        logger.LogInformation("{Job} completed. {Count} Changes found", Name, changesFoundCount);
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Published {Source} with duplicationId {DuplicationId} to topicArn {Topic}"
    )]
    private partial void LogPublishedTreeNode(string source, string? duplicationId, string topic);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Job {JobName} failed validation with validation error '{ValidationError}'"
    )]
    private partial void LogJobFailedValidation(string jobName, string validationError);

    [LoggerMessage(Level = LogLevel.Information, Message = "{JobName} completed. {ChangeCount} changes found")]
    private partial void LogJobComplete(string jobName, int changeCount);
}
