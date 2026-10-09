using System.Threading;
using System.Threading.Tasks;
using Hive.Host.WinForms;
using Xunit;

namespace Hive.Tests;

public sealed class HiveSqlServerInstanceDiscoveryTests
{
    [Fact]
    public async Task Discovery_CoalescesConcurrentScansAndCachesCompletedResults()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var calls = 0;
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult(true);
                release.Wait();
                return new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" });
            },
            networkWaitTimeout: TimeSpan.FromSeconds(10));

        var first = coordinator.DiscoverAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = coordinator.DiscoverAsync(forceRefresh: true);
        release.Set();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(1, calls);
        Assert.All(results, result =>
        {
            Assert.Contains("localhost", result.Instances);
            Assert.Contains(@"REMOTE01\REPORTING", result.Instances);
            Assert.True(result.IsComplete);
        });
        Assert.True((await coordinator.DiscoverAsync()).IsFromCache);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Discovery_ExpiresTtlAndForcedRefreshBypassesCache()
    {
        var now = new DateTimeOffset(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
        var calls = 0;
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                var call = Interlocked.Increment(ref calls);
                return new SqlServerInstanceDiscoveryInventory(new[] { $@"REMOTE{call}\SQL" });
            },
            networkCacheTtl: TimeSpan.FromMinutes(1),
            utcNow: () => now);

        await coordinator.DiscoverAsync();
        Assert.True((await coordinator.DiscoverAsync()).IsFromCache);
        Assert.Equal(1, calls);
        now = now.AddMinutes(2);
        Assert.False((await coordinator.DiscoverAsync()).IsFromCache);
        Assert.Equal(2, calls);
        Assert.False((await coordinator.DiscoverAsync(forceRefresh: true)).IsFromCache);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task Discovery_PreservesLocalResultsWhenLocalInventoryIsIncomplete()
    {
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(
                new[] { "MSSQLSERVER", "HiveSql" },
                IsComplete: false,
                FailureReason: "RegistryView"),
            () => new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" }));

        var result = await coordinator.DiscoverAsync();
        Assert.False(result.IsComplete);
        Assert.Contains("localhost", result.Instances);
        Assert.Contains(@"localhost\HiveSql", result.Instances);
        Assert.Contains(@"REMOTE01\REPORTING", result.Instances);
        Assert.Contains("Local instance discovery was incomplete", result.StatusMessage);
    }

    [Fact]
    public async Task Discovery_RetainsLocalResultsWhenNetworkEnumerationThrows()
    {
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () => throw new IOException("simulated network discovery failure"));

        var result = await coordinator.DiscoverAsync();
        Assert.False(result.IsComplete);
        Assert.Contains("localhost", result.Instances);
        Assert.Contains("Network discovery was incomplete", result.StatusMessage);
    }

    [Fact]
    public async Task Discovery_TimeoutDoesNotCreateAnOverlappingNetworkScan()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var calls = 0;
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult(true);
                release.Wait();
                finished.TrySetResult(true);
                return new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" });
            },
            networkWaitTimeout: TimeSpan.FromSeconds(2),
            delay: static (_, _) => Task.CompletedTask);

        var timedOut = await coordinator.DiscoverAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(timedOut.IsTimedOut);
        Assert.True(timedOut.NetworkScanStillRunning);
        Assert.Contains("localhost", timedOut.Instances);

        var second = await coordinator.DiscoverAsync(forceRefresh: true);
        Assert.True(second.IsTimedOut);
        Assert.Equal(1, calls);
        release.Set();
        await finished.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var completed = await coordinator.DiscoverAsync();
        Assert.False(completed.IsTimedOut);
        Assert.Contains(@"REMOTE01\REPORTING", completed.Instances);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Discovery_CancellingWaiterDoesNotCancelSharedNetworkScan()
    {
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var calls = 0;
        var coordinator = new SqlServerInstanceDiscoveryCoordinator(
            () => new SqlServerInstanceDiscoveryInventory(new[] { "MSSQLSERVER" }),
            () =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult(true);
                release.Wait();
                return new SqlServerInstanceDiscoveryInventory(new[] { @"REMOTE01\REPORTING" });
            },
            networkWaitTimeout: TimeSpan.FromSeconds(10));

        using var cancellation = new CancellationTokenSource();
        var cancelledWait = coordinator.DiscoverAsync(cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await cancelledWait);

        var survivingWait = coordinator.DiscoverAsync(forceRefresh: true);
        release.Set();
        var result = await survivingWait.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(result.IsComplete);
        Assert.Equal(1, calls);
        Assert.Contains(@"REMOTE01\REPORTING", result.Instances);
    }
}
