namespace Infrastructure.Scheduler;

public sealed class JobSettings
{
    public string Cron { get; set; } = "* * * * *";

    public int MaxRetries { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 2;
}
