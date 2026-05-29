using AwesomeAssertions;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using NSubstitute;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Jobs;

namespace TradeGatewayPublisher.Tests.Jobs;

public class TracesIntraChangesJobTests
{
    private readonly ITracesGateway _gateway = Substitute.For<ITracesGateway>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();
    private readonly IOptions<TracesUpdatePublisherOptions> _options;
    private readonly TracesIntraChangesJob _sut;

    public TracesIntraChangesJobTests()
    {
        _options = Options.Create(
            new TracesUpdatePublisherOptions
            {
                IntraTopicArn = "test-topic",
                IntraInternalTopicArn = "test-internal-topic",
            }
        );

        _sut = new TracesIntraChangesJob(_gateway, _sns, _options);
    }

    [Fact]
    public async Task ExecuteAsync_should_publish_all_updates_from_first_page()
    {
        // Arrange
        var context = CreateContext();

        var updates = new[]
        {
            new FindIntraUpdatesResponseRecord("1", DateTime.UtcNow),
            new FindIntraUpdatesResponseRecord("2", DateTime.UtcNow),
        };

        _gateway
            .FindIntraUpdates(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 100, 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FindIntraUpdatesResponse(updates.ToList())));

        _gateway
            .FindIntraUpdates(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 100, 100, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new FindIntraUpdatesResponse(Array.Empty<FindIntraUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _sns.Received(2)
            .PublishAsync("test-topic", Arg.Any<IMessage>(), Arg.Any<Dictionary<string, string>>(), Arg.Any<string>(), cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_should_stop_when_no_updates_returned()
    {
        // Arrange
        var context = CreateContext();

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(new FindIntraUpdatesResponse(Array.Empty<FindIntraUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _sns.DidNotReceive()
            .PublishAsync(
                Arg.Any<string>(),
                Arg.Any<IMessage>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_should_use_watermark_range()
    {
        // Arrange
        var watermark = new WatermarkContext(
            new DateTimeOffset(2024, 01, 01, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 01, 02, 0, 0, 0, TimeSpan.Zero)
        );

        var context = CreateContext(watermark);

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(new FindIntraUpdatesResponse(Array.Empty<FindIntraUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _gateway
            .Received(1)
            .FindIntraUpdates(
                watermark.Watermark.UtcDateTime,
                watermark.Now.UtcDateTime,
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_should_increment_offset_per_page()
    {
        // Arrange
        var context = CreateContext();

        var callOffsets = new List<int>();

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
            {
                var offset = call.ArgAt<int>(3);
                callOffsets.Add(offset);

                return Task.FromResult(
                    new FindIntraUpdatesResponse([new FindIntraUpdatesResponseRecord("1", DateTime.UtcNow)])
                );
            });

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                200,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(new FindIntraUpdatesResponse(Array.Empty<FindIntraUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        callOffsets.Should().ContainInOrder(0, 100);
    }

    private static JobContext CreateContext(WatermarkContext? watermark = null)
    {
        var context = new JobContext("JobId", "TestJob");

        watermark ??= new WatermarkContext(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow);

        context.Set(watermark);

        return context;
    }
}
