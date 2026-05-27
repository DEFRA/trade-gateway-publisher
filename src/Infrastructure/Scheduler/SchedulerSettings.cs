namespace Infrastructure.Scheduler;

public sealed class SchedulerSettings
{
    public const string SectionName = "Scheduler";

    public int MaxConcurrentJobs { get; set; } = 1;

    public Dictionary<string, JobSettings> Jobs { get; set; } = [];
}
