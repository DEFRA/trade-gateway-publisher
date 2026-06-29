using AwesomeAssertions;
using Infrastructure.Leasing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TradeGatewayPublisher.Jobs.Middleware;

namespace TradeGatewayPublisher.Tests.Jobs.Middleware;

public class JobLeaseMiddlewareTests
{
    private readonly ILeaseProvider _leaseProvider = Substitute.For<ILeaseProvider>();
    private readonly JobLeaseMiddleware _sut;

    public JobLeaseMiddlewareTests()
    {
        _sut = new JobLeaseMiddleware(_leaseProvider, NullLogger<JobLeaseMiddleware>.Instance);
    }

    [Fact]
    public async Task InvokeAsync_should_run_next_when_lease_is_acquired()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob", new JobSettings());

        var lease = Substitute.For<IAsyncDisposable>();

        _leaseProvider
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(lease);

        var nextCalled = false;

        JobExecutionDelegate next = (_, _) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_should_skip_next_when_lease_is_null()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "TestJob", new JobSettings());

        _leaseProvider
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns((IAsyncDisposable?)null);

        var nextCalled = false;

        JobExecutionDelegate next = (_, _) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        nextCalled.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_should_use_correct_lease_name()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "MyJob", new JobSettings());

        _leaseProvider
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAsyncDisposable>());

        JobExecutionDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        await _leaseProvider
            .Received(1)
            .TryAcquireAsync("job:MyJob", Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvokeAsync_should_use_default_lease_duration()
    {
        // Arrange
        var context = new JobContext(Guid.NewGuid().ToString(), "MyJob", new JobSettings());

        _leaseProvider
            .TryAcquireAsync(Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IAsyncDisposable>());

        JobExecutionDelegate next = (_, _) => Task.CompletedTask;

        // Act
        await _sut.InvokeAsync(context, CancellationToken.None, next);

        // Assert
        await _leaseProvider
            .Received(1)
            .TryAcquireAsync(Arg.Any<string>(), TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>());
    }
}
