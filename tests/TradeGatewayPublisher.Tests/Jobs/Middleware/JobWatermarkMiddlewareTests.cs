using AwesomeAssertions;
using Infrastructure.Scheduler;
using Infrastructure.Watermark;
using NSubstitute;
using TradeGatewayPublisher.Jobs.Middleware;

namespace TradeGatewayPublisher.Tests.Jobs.Middleware;

public class JobWatermarkMiddlewareTests
{
    private readonly IJobWatermarkStore _store = Substitute.For<IJobWatermarkStore>();
    private readonly JobWatermarkMiddleware _sut;

    public JobWatermarkMiddlewareTests()
    {
        _sut = new JobWatermarkMiddleware(_store);
    }

    [Fact]
    public async Task InvokeAsync_should_use_stored_watermark_when_available()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob");

        var stored = new DateTimeOffset(2024, 01, 01, 12, 0, 0, TimeSpan.Zero);

        _store.GetAsync("TestJob", Arg.Any<CancellationToken>()).Returns(stored);

        var nextCalled = false;

        JobExecutionDelegate next = (ctx, _) =>
        {
            var watermark = ctx.Get<WatermarkContext>();
            watermark!.Watermark.Should().Be(stored);

            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        nextCalled.Should().BeTrue();

        await _store.Received(1).SetAsync("TestJob", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_should_use_default_watermark_when_none_exists()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob");

        _store.GetAsync("TestJob", Arg.Any<CancellationToken>()).Returns((DateTimeOffset?)null);

        var before = DateTimeOffset.UtcNow;

        var nextCalled = false;

        JobExecutionDelegate next = (ctx, _) =>
        {
            var watermark = ctx.Get<WatermarkContext>();

            watermark!.Watermark.Should().BeCloseTo(before.AddMinutes(-5), TimeSpan.FromSeconds(5));

            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_should_set_watermark_in_context_before_next()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob");

        _store.GetAsync("TestJob", Arg.Any<CancellationToken>()).Returns(DateTimeOffset.UtcNow);

        WatermarkContext? captured = null;

        JobExecutionDelegate next = (ctx, _) =>
        {
            captured = ctx.Get<WatermarkContext>();
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        captured.Should().NotBeNull();
        captured!.Now.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task InvokeAsync_should_persist_new_watermark_after_execution()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob");

        _store.GetAsync("TestJob", Arg.Any<CancellationToken>()).Returns(DateTimeOffset.UtcNow);

        JobExecutionDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        await _store.Received(1).SetAsync("TestJob", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
