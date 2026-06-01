#nullable enable
using Infrastructure.Data.Entities;
using Infrastructure.Leasing;
using Microsoft.Extensions.Logging.Abstractions;
using Testing.Data;
using Xunit.Abstractions;

namespace Infrastructure.Tests.Leasing;

public class LeaseProviderTests
{
    /// <summary>
    /// Arrange: Empty lease collection.
    /// Act: Acquire a lease.
    /// Assert: Lease is successfully acquired and persisted.
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WhenLeaseDoesNotExist_ReturnsLeaseHandle()
    {
        // Arrange
        var collection = new FakeCollection<LeaseEntity>();

        var sut = new LeaseProvider(collection.Collection, NullLogger<LeaseProvider>.Instance);

        // Act
        var result = await sut.TryAcquireAsync("test-lease", TimeSpan.FromMinutes(5), CancellationToken.None);

        // Assert
        Assert.NotNull(result);

        var persisted = collection._items.Find(x => x.Id == "test-lease");

        Assert.NotNull(persisted);
        Assert.Equal("test-lease", persisted!.Id);
        Assert.NotNull(persisted.Owner);
        Assert.NotEqual(string.Empty, persisted.Owner);

        Assert.True(persisted.ExpiresAt > DateTime.UtcNow, "Lease expiration should be in the future.");
    }

    /// <summary>
    /// Arrange: Existing lease with the same name.
    /// Act: Attempt to acquire the same lease again.
    /// Assert: Acquisition fails and returns null.
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_WhenLeaseAlreadyExists_ReturnsNull()
    {
        // Arrange
        var existingLease = new LeaseEntity
        {
            Id = "existing-lease",
            Owner = "existing-owner",
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
        };

        var collection = new FakeCollection<LeaseEntity>();
        collection.Add(existingLease);

        var sut = new LeaseProvider(collection.Collection, NullLogger<LeaseProvider>.Instance);

        // Act
        var result = await sut.TryAcquireAsync("existing-lease", TimeSpan.FromMinutes(5), CancellationToken.None);

        // Assert
        Assert.Null(result);

        var persisted = collection._items.Find(x => x.Id == "existing-lease");

        Assert.NotNull(persisted);
        Assert.Equal("existing-owner", persisted!.Owner);
    }

    /// <summary>
    /// Arrange: Multiple leases in collection.
    /// Act: Acquire a different lease.
    /// Assert: Existing leases remain unchanged.
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_DoesNotModifyOtherLeases()
    {
        // Arrange
        var existingLease = new LeaseEntity
        {
            Id = "lease-a",
            Owner = "owner-a",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
        };

        var collection = new FakeCollection<LeaseEntity>();
        collection.Add(existingLease);

        var sut = new LeaseProvider(collection.Collection, NullLogger<LeaseProvider>.Instance);

        // Act
        var result = await sut.TryAcquireAsync("lease-b", TimeSpan.FromMinutes(5), CancellationToken.None);

        // Assert
        Assert.NotNull(result);

        var leaseA = collection._items.Find(x => x.Id == "lease-a");
        var leaseB = collection._items.Find(x => x.Id == "lease-b");

        Assert.NotNull(leaseA);
        Assert.NotNull(leaseB);

        Assert.Equal("owner-a", leaseA!.Owner);
        Assert.Equal("lease-b", leaseB!.Id);
    }

    /// <summary>
    /// Arrange: Valid lease duration.
    /// Act: Acquire lease.
    /// Assert: Expiration is approximately now + duration.
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_SetsExpectedExpiration()
    {
        // Arrange
        var collection = new FakeCollection<LeaseEntity>();

        var sut = new LeaseProvider(collection.Collection, NullLogger<LeaseProvider>.Instance);

        var duration = TimeSpan.FromMinutes(10);

        var before = DateTime.UtcNow;

        // Act
        await sut.TryAcquireAsync("timed-lease", duration, CancellationToken.None);

        var after = DateTime.UtcNow;

        // Assert
        var persisted = collection._items.Find(x => x.Id == "timed-lease");

        Assert.NotNull(persisted);

        var minExpected = before.Add(duration);
        var maxExpected = after.Add(duration);

        Assert.True(
            persisted!.ExpiresAt >= minExpected && persisted.ExpiresAt <= maxExpected,
            $"Expected expiration between {minExpected:o} and {maxExpected:o} but found {persisted.ExpiresAt:o}"
        );
    }

    /// <summary>
    /// Arrange: Lease acquisition succeeds.
    /// Act: Dispose returned handle.
    /// Assert: Lease record is removed from collection.
    /// </summary>
    [Fact]
    public async Task TryAcquireAsync_DisposingHandle_ReleasesLease()
    {
        // Arrange
        var collection = new FakeCollection<LeaseEntity>();

        var sut = new LeaseProvider(collection.Collection, NullLogger<LeaseProvider>.Instance);

        // Act
        var handle = await sut.TryAcquireAsync("disposable-lease", TimeSpan.FromMinutes(5), CancellationToken.None);

        Assert.NotNull(handle);

        await handle!.DisposeAsync();

        // Assert
        var persisted = collection._items.Find(x => x.Id == "disposable-lease");

        Assert.Null(persisted);
    }
}
