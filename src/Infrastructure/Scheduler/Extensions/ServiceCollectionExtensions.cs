using Infrastructure.Leasing;
using Infrastructure.Watermark;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Scheduler.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SchedulerSettings>()
            .Bind(configuration.GetSection(SchedulerSettings.SectionName))
            .ValidateOnStart();

        services.AddHostedService<SchedulerBackgroundService>();
        services.AddScoped<IJobExecutor, JobExecutor>();
        services.AddScoped<IJobWatermarkStore, JobWatermarkStore>();
        services.AddScoped<ILeaseProvider, LeaseProvider>();

        return services;
    }
}
