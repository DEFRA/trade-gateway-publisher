using AwesomeAssertions;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Infrastructure.TracesGateway;
using Microsoft.Extensions.Options;
using NSubstitute;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Features.ChedChanges;
using TradeGatewayPublisher.Features.IntraChanges;

namespace TradeGatewayPublisher.Tests.Features.ChedChanges;

public class TracesChedChangesJobTests
{
    private readonly ITracesGateway _gateway = Substitute.For<ITracesGateway>();
    private readonly ISnsPublisher _sns = Substitute.For<ISnsPublisher>();
    private readonly IOptions<TracesUpdatePublisherOptions> _options;
    private readonly TracesChedChangesJob _sut;

    public TracesChedChangesJobTests()
    {
        _options = Options.Create(
            new TracesUpdatePublisherOptions
            {
                IntraTopicArn = "test-topic",
                IntraInternalTopicArn = "test-internal-topic",
                ChedTopicArn = "test-ched-topic",
                ChedInternalTopicArn = "test-ched-internal-topic",
            }
        );

        _sut = new TracesChedChangesJob(_gateway, _sns, _options);
    }

    [Fact]
    public async Task ExecuteAsync_should_publish_all_updates_from_first_page()
    {
        // Arrange
        var context = CreateContext();

        var updates = new[]
        {
            new FindChedUpdatesResponseRecord("1", DateTime.UtcNow),
            new FindChedUpdatesResponseRecord("2", DateTime.UtcNow),
        };

        _gateway
            .FindChedUpdates(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 100, 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new FindChedUpdatesResponse(updates.ToList())));

        _gateway
            .FindChedUpdates(Arg.Any<DateTime>(), Arg.Any<DateTime>(), 100, 100, Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(new FindChedUpdatesResponse(Array.Empty<FindChedUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _sns.Received(2)
            .PublishAsync(
                "test-ched-internal-topic",
                Arg.Any<IMessage>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_should_stop_when_no_updates_returned()
    {
        // Arrange
        var context = CreateContext();

        _gateway
            .FindChedUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(new FindChedUpdatesResponse(Array.Empty<FindChedUpdatesResponseRecord>().ToList()))
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
            .FindChedUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(new FindChedUpdatesResponse(Array.Empty<FindChedUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _gateway
            .Received(1)
            .FindChedUpdates(
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
            .FindChedUpdates(
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

                var response = new FindChedUpdatesResponse([]);
                for (var i = 0; i < 100; i++)
                {
                    response.Items.Add(new FindChedUpdatesResponseRecord((offset + 1).ToString(), DateTime.UtcNow));
                }

                return Task.FromResult(response);
            });

        _gateway
            .FindChedUpdates(
                Arg.Any<DateTime>(),
                Arg.Any<DateTime>(),
                Arg.Any<int>(),
                200,
                Arg.Any<CancellationToken>()
            )
            .Returns(
                Task.FromResult(new FindChedUpdatesResponse(Array.Empty<FindChedUpdatesResponseRecord>().ToList()))
            );

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        callOffsets.Should().ContainInOrder(0, 100);
    }

    private static JobContext CreateContext(WatermarkContext? watermark = null)
    {
        var context = new JobContext("JobId", "TestJob", new JobSettings());

        watermark ??= new WatermarkContext(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow);

        context.Set(watermark);

        return context;
    }
}
