using Infrastructure.Data.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Authentication.AWS;

namespace Infrastructure.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<MongoDbOptions>()
            .Bind(configuration.GetSection(MongoDbOptions.SectionName))
            .ValidateOnStart();

        services.AddSingleton(sp =>
        {
            MongoClientSettings.Extensions.AddAWSAuthentication();

            var options =
                sp.GetService<IOptions<MongoDbOptions>>() ?? throw new InvalidOperationException("Options not found");
            var settings = MongoClientSettings.FromConnectionString(options.Value.DatabaseUri);

            var client = new MongoClient(settings);
            var conventionPack = new ConventionPack
            {
                new CamelCaseElementNameConvention(),
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(true),
            };

            ConventionRegistry.Register(nameof(conventionPack), conventionPack, _ => true);

            return client.GetDatabase(options.Value.DatabaseName);
        });

        services.AddSingleton<IMongoCollection<LeaseEntity>>(sp =>
        {
            var collection = sp.GetRequiredService<IMongoDatabase>().GetCollection<LeaseEntity>("leases");

            var indexModel = new CreateIndexModel<LeaseEntity>(
                Builders<LeaseEntity>.IndexKeys.Ascending(x => x.ExpiresAt),
                new CreateIndexOptions
                {
                    Name = "ExpiresAtTtlIdx",
                    Background = true,
                    ExpireAfter = TimeSpan.FromMinutes(5),
                }
            );
            try
            {
                collection.Indexes.CreateOne(indexModel);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return collection;
        });

        services.AddSingleton<IMongoCollection<JobWatermarkEntity>>(sp =>
            sp.GetRequiredService<IMongoDatabase>().GetCollection<JobWatermarkEntity>("job_watermarks")
        );

        return services;
    }
}
