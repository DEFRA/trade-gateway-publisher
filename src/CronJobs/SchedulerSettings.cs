namespace CronJobs;

public sealed class SchedulerSettings
{
    public int MaxConcurrentJobs { get; set; } = 1;

    public Dictionary<string, JobSettings> Jobs { get; set; } = [];
}
