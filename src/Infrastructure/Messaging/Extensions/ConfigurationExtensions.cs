using Microsoft.Extensions.Configuration;

namespace Infrastructure.Messaging.Extensions
{
    public static class ConfigurationExtensions
    {
        public static bool FeatureIsEnabled(this IConfiguration configuration, string featureFlag)
        {
            return configuration.GetValue<bool>($"FeatureManagement:{featureFlag}");
        }
    }
}
