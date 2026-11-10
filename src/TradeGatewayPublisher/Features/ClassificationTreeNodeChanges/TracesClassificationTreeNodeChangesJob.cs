using System.Text.Json;
using Defra.TradeImports.Messaging.Publishing;
using Elastic.CommonSchema;
using Infrastructure;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Options;
using Refit;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.ReferenceData;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Utils;

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

                foreach (var rootNode in updatesResponse.Content?.Nodes ?? [])
                {
                    changesFoundCount += await ProcessNode(topicArn, treeId, rootNode, cancellationToken);
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

        LogJobComplete(Name, changesFoundCount);
    }

    private async Task<int> ProcessNode(
        string topicArn,
        string treeId,
        ClassificationTreeNode node,
        CancellationToken cancellationToken
    )
    {
        var updateJson = node.ToJson();
        var duplicationId = updateJson.GenerateHash();

        await snsPublisher.PublishAsync(
            topicArn,
            updateJson,
            headers: new Dictionary<string, string>()
            {
                { ClassificationTreeMessageHeaderKeys.TreeId, treeId },
                { ClassificationTreeMessageHeaderKeys.DuplicationId, duplicationId },
            },
            compressionThresholdBytes: options.Value.ReferenceDataCompressionThresholdBytes,
            cancellationToken: cancellationToken,
            duplicationId: duplicationId
        );

        LogPublishedTreeNode(SourceTag, duplicationId, topicArn);

        var count = 1;

        foreach (var child in node.Children ?? [])
        {
            count += await ProcessNode(topicArn, treeId, child, cancellationToken);
        }

        return count;
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
