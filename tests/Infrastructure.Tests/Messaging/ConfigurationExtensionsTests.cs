using Infrastructure.Messaging.Extensions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Infrastructure.Tests.Messaging;

public class ConfigurationExtensionsTests
{
    [Fact]
    public void FeatureIsEnabled_returns_true_when_feature_flag_is_true()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new[] { new KeyValuePair<string, string?>("FeatureManagement:MyFeature", "true") })
            .Build();

        Assert.True(config.FeatureIsEnabled("MyFeature"));
    }

    [Fact]
    public void FeatureIsEnabled_returns_false_when_feature_flag_missing_or_false()
    {
        var config = new ConfigurationBuilder().Build();
        Assert.False(config.FeatureIsEnabled("MissingFeature"));

        var configFalse = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new[] { new KeyValuePair<string, string?>("FeatureManagement:DisabledFeature", "false") }
            )
            .Build();
        Assert.False(configFalse.FeatureIsEnabled("DisabledFeature"));
    }
}
