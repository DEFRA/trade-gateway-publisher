using CronJobs.Leasing;
using CronJobs.Watermark;
using Data;
using Data.Mongo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;

namespace CronJobs.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<SchedulerSettings>()
            .Bind(configuration.GetSection(SchedulerSettings.SectionName))
            .ValidateOnStart();

        services.AddHostedService<SchedulerBackgroundService>();
        services.AddSingleton<IJobExecutor, JobExecutor>();
        services.AddSingleton<IJobWatermarkStore, MongoJobWatermarkStore>();
        services.AddSingleton<IJobLeaseProvider, JobLeaseProvider>();

        return services;
    }
}
