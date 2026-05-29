using System;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using Infrastructure.Scheduler;
using Infrastructure.Scheduler.Metrics;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using TradeGatewayPublisher.Jobs.Middleware;
using Xunit;

namespace TradeGatewayPublisher.Tests.Jobs.Middleware;

public class JobMetricsMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_Should_Record_Success_Metrics()
    {
        // Arrange

        using var meter = new Meter("Infrastructure.Scheduler");

        using var startedCollector = new MetricCollector<long>(meter, "JobsCount");

        using var durationCollector = new MetricCollector<double>(meter, "JobsDuration");

        var metrics = new JobMetrics(new DummyMeterFactory(meter), meter.Name);

        var sut = new JobMetricsMiddleware(metrics);

        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob");

        JobExecutionDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        var startedMeasurements = startedCollector.GetMeasurementSnapshot();

        startedMeasurements.Should().ContainSingle();

        startedMeasurements[0].Value.Should().Be(1);

        startedMeasurements[0].Tags.Should().Contain(x => x.Key == "JobName" && x.Value!.ToString() == "TestJob");

        var durationMeasurements = durationCollector.GetMeasurementSnapshot();

        durationMeasurements.Should().ContainSingle();

        durationMeasurements[0].Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task InvokeAsync_Should_Record_Fault_Metrics_When_Exception_Is_Thrown()
    {
        // Arrange
        using var meter = new Meter("Infrastructure.Scheduler");
        using var faultedCollector = new MetricCollector<long>(meter, "JobsFaulted");

        var metrics = new JobMetrics(new DummyMeterFactory(meter), meter.Name);

        var sut = new JobMetricsMiddleware(metrics);

        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob");

        var exception = new InvalidOperationException("boom");

        JobExecutionDelegate next = (_, _) => throw exception;

        // Act
        var act = async () => await sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        var faultMeasurements = faultedCollector.GetMeasurementSnapshot();

        faultMeasurements.Should().ContainSingle();

        faultMeasurements[0].Value.Should().Be(1);

        faultMeasurements[0].Tags.Should().Contain(x => x.Key == "JobName" && x.Value!.ToString() == "TestJob");
    }
}

internal sealed class DummyMeterFactory(Meter meter) : IMeterFactory
{
    public Meter Create(MeterOptions options) => meter;

    public void Dispose() { }
}
