using System.Diagnostics.Metrics;
using AwesomeAssertions;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Messaging.Publishing.Middleware;
using Testing;

namespace Infrastructure.Tests.Messaging.Publishing.Middleware;

public class MetricsPublishMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Should_Record_Publish_Metrics()
    {
        // Arrange
        var measurements = new List<long>();

        using var listener = new MeterListener();

        listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "TestMeter")
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>(
            (instrument, measurement, tags, state) =>
            {
                if (instrument.Name == "MessagingPublish")
                {
                    measurements.Add(measurement);
                }
            }
        );

        listener.Start();

        var meterFactory = new TestMeterFactory();
        var metrics = new PublishMetrics(meterFactory, "TestMeter");

        var sut = new MetricsPublishMiddleware(metrics);

        var context = new PublishContext() { TopicName = "orders" };

        // Act
        await sut.InvokeAsync(context, () => Task.CompletedTask);

        // Assert
        measurements.Should().ContainSingle(x => x == 1);
    }
}
