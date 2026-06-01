#nullable enable
using System.Globalization;
using System.Linq.Expressions;
using Infrastructure.Data.Entities;
using Infrastructure.Watermark;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Driver;
using NSubstitute;
using Testing.Data;

namespace Infrastructure.Tests.Watermark;

public class JobWatermarkStoreTests
{
    /// <summary>
    /// Arrange: IMongoCollectionSet contains no entity with the provided jobName.
    /// Act: Call GetAsync with various jobName inputs (empty, whitespace, not present).
    /// Assert: Returned Task result is null indicating no watermark found.
    /// </summary>
    [Theory]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("nonexistent-job", null)]
    [InlineData("other-job", "2026-05-26T00:00:00.0000000+00:00")]
    public async Task GetAsync_NoDocumentFound_ReturnsNull(string jobName, string? expected)
    {
        // Arrange
        var expectedWatermark = string.IsNullOrEmpty(expected)
            ? DateTimeOffset.UtcNow
            : DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture);

        var items = new List<JobWatermarkEntity>
        {
            new JobWatermarkEntity { Id = "other-job", Watermark = expectedWatermark.DateTime },
        };

        var collection = new FakeCollection<JobWatermarkEntity>();
        collection.AddRange(items);

        var sut = new JobWatermarkStore(collection.Collection, NullLogger<JobWatermarkStore>.Instance);

        // Act
        var result = await sut.GetAsync(jobName, CancellationToken.None);

        // Assert
        if (string.IsNullOrEmpty(expected))
            Assert.Null(result);
        else
        {
            Assert.Equal(expectedWatermark, result);
        }
    }

    /// <summary>
    /// Arrange: Empty watermark collection.
    /// Act: Call SetAsync for a new job.
    /// Assert: Watermark is persisted and can be retrieved with GetAsync.
    /// </summary>
    [Fact]
    public async Task SetAsync_NewJob_PersistsWatermark()
    {
        // Arrange
        var collection = new FakeCollection<JobWatermarkEntity>();

        var sut = new JobWatermarkStore(collection.Collection, NullLogger<JobWatermarkStore>.Instance);

        var watermark = DateTimeOffset.Parse("2026-05-26T12:30:45.0000000+00:00", CultureInfo.InvariantCulture);

        // Act
        await sut.SetAsync("test-job", watermark, CancellationToken.None);

        // Assert
        var result = await sut.GetAsync("test-job", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(watermark, result);
    }

    /// <summary>
    /// Arrange: Existing watermark for a job.
    /// Act: Call SetAsync with a newer watermark.
    /// Assert: Existing watermark is updated.
    /// </summary>
    [Fact]
    public async Task SetAsync_ExistingJob_UpdatesWatermark()
    {
        // Arrange
        var originalWatermark = DateTimeOffset.Parse("2026-05-25T00:00:00.0000000+00:00", CultureInfo.InvariantCulture);

        var updatedWatermark = DateTimeOffset.Parse("2026-05-26T18:15:00.0000000+00:00", CultureInfo.InvariantCulture);

        var items = new List<JobWatermarkEntity>
        {
            new() { Id = "existing-job", Watermark = originalWatermark.UtcDateTime },
        };

        var collection = new FakeCollection<JobWatermarkEntity>();
        collection.AddRange(items);

        var sut = new JobWatermarkStore(collection.Collection, NullLogger<JobWatermarkStore>.Instance);

        // Act
        await sut.SetAsync("existing-job", updatedWatermark, CancellationToken.None);

        // Assert
        var result = await sut.GetAsync("existing-job", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(updatedWatermark, result);
    }

    /// <summary>
    /// Arrange: Multiple jobs exist in the collection.
    /// Act: Call SetAsync for one specific job.
    /// Assert: Only the targeted job watermark is changed.
    /// </summary>
    [Fact]
    public async Task SetAsync_OnlyUpdatesSpecifiedJob()
    {
        // Arrange
        var untouchedWatermark = DateTimeOffset.Parse(
            "2026-05-20T08:00:00.0000000+00:00",
            CultureInfo.InvariantCulture
        );

        var newWatermark = DateTimeOffset.Parse("2026-05-26T22:45:00.0000000+00:00", CultureInfo.InvariantCulture);

        var items = new List<JobWatermarkEntity>
        {
            new() { Id = "job-a", Watermark = untouchedWatermark.UtcDateTime },
            new() { Id = "job-b", Watermark = untouchedWatermark.UtcDateTime },
        };

        var collection = new FakeCollection<JobWatermarkEntity>();
        collection.AddRange(items);

        var sut = new JobWatermarkStore(collection.Collection, NullLogger<JobWatermarkStore>.Instance);

        // Act
        await sut.SetAsync("job-b", newWatermark, CancellationToken.None);

        // Assert
        var jobAResult = await sut.GetAsync("job-a", CancellationToken.None);
        var jobBResult = await sut.GetAsync("job-b", CancellationToken.None);

        Assert.Equal(untouchedWatermark, jobAResult);
        Assert.Equal(newWatermark, jobBResult);
    }

    /// <summary>
    /// Arrange: A watermark with a non-UTC offset.
    /// Act: Call SetAsync.
    /// Assert: Stored value is normalized to UTC and retrieved correctly.
    /// </summary>
    [Fact]
    public async Task SetAsync_NormalizesToUtc()
    {
        // Arrange
        var input = DateTimeOffset.Parse("2026-05-26T15:00:00.0000000+02:00", CultureInfo.InvariantCulture);

        var expectedUtc = input.ToUniversalTime();

        var collection = new FakeCollection<JobWatermarkEntity>();

        var sut = new JobWatermarkStore(collection.Collection, NullLogger<JobWatermarkStore>.Instance);

        // Act
        await sut.SetAsync("utc-job", input, CancellationToken.None);

        // Assert
        var result = await sut.GetAsync("utc-job", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(expectedUtc, result);
        Assert.Equal(TimeSpan.Zero, result.Value.Offset);
    }
}
