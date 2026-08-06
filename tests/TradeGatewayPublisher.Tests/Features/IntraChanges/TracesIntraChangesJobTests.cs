using AwesomeAssertions;
using Infrastructure.Messaging;
using Infrastructure.Messaging.Publishing;
using Infrastructure.Scheduler;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Refit;
using System.Net;
using Trade.Gateway.Api.Client.Clients;
using Trade.Gateway.Api.Contract.Certificate;
using TradeGatewayPublisher.Config;
using TradeGatewayPublisher.Features.IntraChanges;

namespace TradeGatewayPublisher.Tests.Features.IntraChanges;

public class TracesIntraChangesJobTests
{
    private readonly ITracesGatewayIntraClient _gateway = Substitute.For<ITracesGatewayIntraClient>();
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
                ChedTopicArn = "test-ched-topic",
                ChedInternalTopicArn = "test-ched-internal-topic",
            }
        );

        _sut = new TracesIntraChangesJob(_gateway, _sns, _options, NullLogger<TracesIntraChangesJob>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_should_publish_all_updates_from_first_page()
    {
        // Arrange
        var context = CreateContext();

        var updates = new[]
        {
            new DefraUNVTDINTRASummaryProfileItem
            {
                Id = "1",
                Origin = "Origin",
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            },
            new DefraUNVTDINTRASummaryProfileItem
            {
                Id = "2",
                Origin = "Origin",
                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow,
            },
        };

        var response = new ApiResponse<DefraUNVTDINTRASummaryProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDINTRASummaryProfile
            {
                Items = updates,
                HasMore = true,
                Offset = 0,
                PageSize = 100
            },
            new RefitSettings()
        );

        _gateway
            .FindIntraUpdates(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 100, 0, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response));

        var response1 = new ApiResponse<DefraUNVTDINTRASummaryProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDINTRASummaryProfile
            {
                Items = [],
                HasMore = false,
                Offset = 0,
                PageSize = 100
            },
            new RefitSettings()
        );

        _gateway
            .FindIntraUpdates(Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 100, 100, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(response1));

        // Act
        await _sut.ExecuteAsync(context, CancellationToken.None);

        // Assert
        await _sns.Received(2)
            .PublishAsync(
                "test-internal-topic",
                Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                cancellationToken: Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task ExecuteAsync_should_stop_when_no_updates_returned()
    {
        // Arrange
        var context = CreateContext();

        var response = new ApiResponse<DefraUNVTDINTRASummaryProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDINTRASummaryProfile
            {
                Items = [],
                HasMore = true,
                Offset = 0,
                PageSize = 100
            },
            new RefitSettings()
        );

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(response));

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

        var response = new ApiResponse<DefraUNVTDINTRASummaryProfile>(
            new HttpResponseMessage(HttpStatusCode.OK),
            new DefraUNVTDINTRASummaryProfile
            {
                Items = [],
                HasMore = true,
                Offset = 0,
                PageSize = 100
            },
            new RefitSettings()
        );

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(response));

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
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(call =>
            {
                var offset = call.ArgAt<int>(3);
                callOffsets.Add(offset);

                var list = new List<DefraUNVTDINTRASummaryProfileItem>();
                for (var i = 0; i < 100; i++)
                {
                    list.Add(new DefraUNVTDINTRASummaryProfileItem
                    {
                        Id = (offset + 1).ToString(),
                        Origin = "Origin",
                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow,
                    });
                }


                var response = new ApiResponse<DefraUNVTDINTRASummaryProfile>(
                    new HttpResponseMessage(HttpStatusCode.OK),
                    new DefraUNVTDINTRASummaryProfile
                    {
                        Items = list.ToArray(),
                        HasMore = true,
                        Offset = 0,
                        PageSize = 100
                    },
                    new RefitSettings()
                );

                return Task.FromResult(response);
            });

        _gateway
            .FindIntraUpdates(
                Arg.Any<DateTimeOffset>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<int>(),
                200,
                Arg.Any<CancellationToken>()
            )
            .Returns(Task.FromResult(new ApiResponse<DefraUNVTDINTRASummaryProfile>(
                new HttpResponseMessage(HttpStatusCode.OK),
                new DefraUNVTDINTRASummaryProfile
                {
                    Items = [],
                    HasMore = true,
                    Offset = 0,
                    PageSize = 100
                },
                new RefitSettings()
            )));

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
