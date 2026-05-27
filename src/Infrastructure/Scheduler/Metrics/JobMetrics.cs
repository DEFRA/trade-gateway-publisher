using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace Infrastructure.Scheduler.Metrics;

[ExcludeFromCodeCoverage]
public class JobMetrics
{
    private readonly Histogram<double> _jobDuration;
    private readonly Counter<long> _jobTotal;
    private readonly Counter<long> _jobFaultTotal;
    private readonly Counter<long> _jobInProgress;

    public JobMetrics(IMeterFactory meterFactory, string meterName)
    {
        var meter = meterFactory.Create(meterName);

        _jobTotal = meter.CreateCounter<long>("JobsCount", "COUNT", description: "Number of jobs completed");
        _jobFaultTotal = meter.CreateCounter<long>("JobsFaulted", "COUNT", description: "Number of jobs faults");

        _jobInProgress = meter.CreateCounter<long>("JobsActive", "COUNT", description: "Number of jobs in progress");
        _jobDuration = meter.CreateHistogram<double>(
            "JobsDuration",
            "MILLISECONDS",
            "Elapsed time spent executing a job, in millis"
        );
    }

    public void Start(string jobName)
    {
        var tagList = BuildTags(jobName);

        _jobTotal.Add(1, tagList);
        _jobInProgress.Add(1, tagList);
    }

    public void Faulted(string jobName, Exception exception)
    {
        var tagList = BuildTags(jobName);

        tagList.Add(Constants.Tags.ExceptionType, exception.GetType().Name);
        _jobFaultTotal.Add(1, tagList);
    }

    public void Complete(string jobName, double milliseconds)
    {
        var tagList = BuildTags(jobName);

        _jobInProgress.Add(-1, tagList);
        _jobDuration.Record(milliseconds, tagList);
    }

    private static TagList BuildTags(string jobName)
    {
        return new TagList
        {
            { Constants.Tags.Service, Process.GetCurrentProcess().ProcessName },
            { Constants.Tags.JobName, jobName },
        };
    }

    private static class Constants
    {
        public static class Tags
        {
            public const string JobName = "JobName";
            public const string Service = "ServiceName";
            public const string ExceptionType = "ExceptionType";
        }
    }
}
