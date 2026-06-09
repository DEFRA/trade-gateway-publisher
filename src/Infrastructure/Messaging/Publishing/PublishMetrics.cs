using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Infrastructure.Messaging.Publishing;

[ExcludeFromCodeCoverage]
public class PublishMetrics
{
    private readonly Histogram<double> _publishDuration;
    private readonly Counter<long> _publishTotal;
    private readonly Counter<long> _publishFaultTotal;
    private readonly Counter<long> _publishInProgress;

    public PublishMetrics(IMeterFactory meterFactory, string meterName)
    {
        var meter = meterFactory.Create(meterName);

        _publishTotal = meter.CreateCounter<long>(
            "MessagingPublish",
            "COUNT",
            description: "Number of messages published"
        );
        _publishFaultTotal = meter.CreateCounter<long>(
            "MessagingPublishErrors",
            "COUNT",
            description: "Number of message publish faults"
        );

        _publishInProgress = meter.CreateCounter<long>(
            "MessagingPublishActive",
            "COUNT",
            description: "Number of publishing in progress"
        );
        _publishDuration = meter.CreateHistogram<double>(
            "MessagingPublishDuration",
            "MILLISECONDS",
            "Elapsed time spent publishing a message, in millis"
        );
    }

    public void Start(string topicName)
    {
        var tagList = BuildTags(topicName);

        _publishTotal.Add(1, tagList);
        _publishInProgress.Add(1, tagList);
    }

    public void Faulted(string topicName, Exception exception)
    {
        var tagList = BuildTags(topicName);

        tagList.Add(Constants.Tags.ExceptionType, exception.GetType().Name);
        _publishFaultTotal.Add(1, tagList);
    }

    public void Complete(string topicName, double milliseconds)
    {
        var tagList = BuildTags(topicName);

        _publishInProgress.Add(-1, tagList);
        _publishDuration.Record(milliseconds, tagList);
    }

    private static TagList BuildTags(string topicName)
    {
        return new TagList
        {
            { Constants.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Constants.Tags.TopicName, topicName },
        };
    }

    private static class Constants
    {
        public static class Tags
        {
            public const string TopicName = "TopicName";
            public const string Service = "ServiceName";
            public const string ExceptionType = "ExceptionType";
        }
    }
}
