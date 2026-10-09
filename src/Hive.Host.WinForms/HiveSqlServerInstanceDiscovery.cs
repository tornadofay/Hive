using System.Data;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sql;
using Microsoft.Win32;

namespace Hive.Host.WinForms;

internal sealed record SqlServerInstanceDiscoveryInventory(
    IReadOnlyList<string> Instances,
    bool IsComplete = true,
    string? FailureReason = null);

internal enum SqlServerInstanceDiscoveryUpdateKind
{
    LocalResultsAvailable,
    TimedOut,
    Completed
}

internal sealed record SqlServerInstanceDiscoveryResult(
    IReadOnlyList<string> Instances,
    bool IsComplete,
    bool IsTimedOut,
    bool IsFromCache,
    bool NetworkScanStillRunning,
    string StatusMessage);

internal sealed record SqlServerInstanceDiscoveryUpdate(
    SqlServerInstanceDiscoveryUpdateKind Kind,
    SqlServerInstanceDiscoveryResult Result,
    bool IsSearching);

/// <summary>
/// Shares discovery results across Persistence views. SQL client network
/// enumeration is synchronous and cannot be interrupted after it starts, so only
/// one dedicated worker may execute it at a time.
/// </summary>
internal sealed class SqlServerInstanceDiscoveryCoordinator
{
    private static readonly SqlServerInstanceDiscoveryInventory EmptyInventory =
        new(Array.Empty<string>(), IsComplete: false, FailureReason: "Not yet discovered.");

    private readonly object _gate = new();
    private readonly Func<SqlServerInstanceDiscoveryInventory> _discoverLocal;
    private readonly Func<SqlServerInstanceDiscoveryInventory> _discoverNetwork;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly TimeSpan _localCacheTtl;
    private readonly TimeSpan _networkCacheTtl;
    private readonly TimeSpan _failureCacheTtl;
    private readonly TimeSpan _networkWaitTimeout;

    private CachedInventory? _cachedLocal;
    private CachedInventory? _cachedNetwork;
    private Task<SqlServerInstanceDiscoveryInventory>? _localScan;
    private Task<SqlServerInstanceDiscoveryInventory>? _networkScan;

    internal event Action<SqlServerInstanceDiscoveryResult>? NetworkResultsCompleted;

    internal SqlServerInstanceDiscoveryCoordinator(
        Func<SqlServerInstanceDiscoveryInventory> discoverLocal,
        Func<SqlServerInstanceDiscoveryInventory> discoverNetwork,
        TimeSpan? localCacheTtl = null,
        TimeSpan? networkCacheTtl = null,
        TimeSpan? failureCacheTtl = null,
        TimeSpan? networkWaitTimeout = null,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _discoverLocal = discoverLocal ?? throw new ArgumentNullException(nameof(discoverLocal));
        _discoverNetwork = discoverNetwork ?? throw new ArgumentNullException(nameof(discoverNetwork));
        _localCacheTtl = localCacheTtl ?? TimeSpan.FromMinutes(5);
        _networkCacheTtl = networkCacheTtl ?? TimeSpan.FromMinutes(1);
        _failureCacheTtl = failureCacheTtl ?? TimeSpan.FromSeconds(15);
        _networkWaitTimeout = networkWaitTimeout ?? TimeSpan.FromSeconds(3);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? static (duration, token) => Task.Delay(duration, token);

        if (_localCacheTtl <= TimeSpan.Zero ||
            _networkCacheTtl <= TimeSpan.Zero ||
            _failureCacheTtl <= TimeSpan.Zero ||
            _networkWaitTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(networkWaitTimeout),
                "Discovery cache lifetimes and wait timeout must be positive.");
        }
    }

    internal async Task<SqlServerInstanceDiscoveryResult> DiscoverAsync(
        IProgress<SqlServerInstanceDiscoveryUpdate>? progress = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Local inventory starts independently from the network scan.
        var localTicket = GetLocalScan(forceRefresh);
        var networkTicket = GetNetworkScan(forceRefresh);
        var local = await localTicket.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var waitingForNetwork = !networkTicket.IsFromCache && !networkTicket.Task.IsCompleted;
        if (waitingForNetwork)
        {
            var localOnly = CreateResult(
                local,
                GetLastKnownNetworkInventory() ?? EmptyInventory,
                isTimedOut: false,
                isFromCache: false,
                networkScanStillRunning: true) with
            {
                StatusMessage = HiveSqlServerInstanceDiscovery.SearchingStatusMessage
            };
            ReportSafely(
                progress,
                new SqlServerInstanceDiscoveryUpdate(
                    SqlServerInstanceDiscoveryUpdateKind.LocalResultsAvailable,
                    localOnly,
                    IsSearching: true));
        }

        if (!waitingForNetwork)
        {
            var network = await networkTicket.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var completed = CreateResult(
                local,
                network,
                isTimedOut: false,
                isFromCache: localTicket.IsFromCache && networkTicket.IsFromCache,
                networkScanStillRunning: false);
            ReportSafely(progress, new(SqlServerInstanceDiscoveryUpdateKind.Completed, completed, false));
            return completed;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutTask = _delay(_networkWaitTimeout, timeoutCts.Token);
        var winner = await Task.WhenAny(networkTicket.Task, timeoutTask).ConfigureAwait(false);

        if (winner == networkTicket.Task || networkTicket.Task.IsCompleted)
        {
            timeoutCts.Cancel();
            var network = await networkTicket.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            var completed = CreateResult(local, network, false, false, false);
            ReportSafely(progress, new(SqlServerInstanceDiscoveryUpdateKind.Completed, completed, false));
            return completed;
        }

        // This bounds only the caller's wait. Cancellation cannot terminate a
        // SqlDataSourceEnumerator call that is already running.
        await timeoutTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (networkTicket.Task.IsCompleted)
        {
            var network = await networkTicket.Task.ConfigureAwait(false);
            var completed = CreateResult(local, network, false, false, false);
            ReportSafely(progress, new(SqlServerInstanceDiscoveryUpdateKind.Completed, completed, false));
            return completed;
        }

        var timedOut = CreateResult(
            local,
            GetLastKnownNetworkInventory() ?? EmptyInventory,
            isTimedOut: true,
            isFromCache: false,
            networkScanStillRunning: true);
        ReportSafely(progress, new(SqlServerInstanceDiscoveryUpdateKind.TimedOut, timedOut, false));
        return timedOut;
    }

    private (Task<SqlServerInstanceDiscoveryInventory> Task, bool IsFromCache) GetLocalScan(bool forceRefresh)
    {
        lock (_gate)
        {
            // A request joins an in-flight/completing scan even when forced. It
            // never starts a second scan because the first wait timed out.
            if (_localScan is { } running)
                return (running, false);

            if (!forceRefresh && IsFresh(_cachedLocal, GetTtl(_cachedLocal?.Inventory, _localCacheTtl)))
                return (Task.FromResult(_cachedLocal!.Inventory), true);

            var scan = StartDedicatedScan(_discoverLocal, "local");
            _localScan = scan;
            _ = scan.ContinueWith(
                completed => CacheLocalScan(completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return (scan, false);
        }
    }

    private (Task<SqlServerInstanceDiscoveryInventory> Task, bool IsFromCache) GetNetworkScan(bool forceRefresh)
    {
        lock (_gate)
        {
            if (_networkScan is { } running)
                return (running, false);

            if (!forceRefresh && IsFresh(_cachedNetwork, GetTtl(_cachedNetwork?.Inventory, _networkCacheTtl)))
                return (Task.FromResult(_cachedNetwork!.Inventory), true);

            var scan = StartDedicatedScan(_discoverNetwork, "network");
            _networkScan = scan;
            _ = scan.ContinueWith(
                completed => CacheNetworkScan(completed),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return (scan, false);
        }
    }

    private Task<SqlServerInstanceDiscoveryInventory> StartDedicatedScan(
        Func<SqlServerInstanceDiscoveryInventory> discover,
        string inventoryName) =>
        Task.Factory.StartNew(
            () => InvokeDiscovery(discover, inventoryName),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

    private static SqlServerInstanceDiscoveryInventory InvokeDiscovery(
        Func<SqlServerInstanceDiscoveryInventory> discover,
        string inventoryName)
    {
        try
        {
            var result = discover();
            var instances = result.Instances
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (!result.IsComplete)
                Trace.TraceWarning("SQL Server {0} discovery incomplete ({1}).", inventoryName, result.FailureReason ?? "unspecified");
            return result with { Instances = instances };
        }
        catch (Exception exception)
        {
            Trace.TraceWarning("SQL Server {0} discovery failed ({1}).", inventoryName, exception.GetType().Name);
            return new SqlServerInstanceDiscoveryInventory(
                Array.Empty<string>(),
                IsComplete: false,
                FailureReason: exception.GetType().Name);
        }
    }

    private void CacheLocalScan(Task<SqlServerInstanceDiscoveryInventory> completed)
    {
        var inventory = ReadCompletedScan(completed, "local");
        lock (_gate)
        {
            if (!ReferenceEquals(_localScan, completed))
                return;

            _cachedLocal = new CachedInventory(inventory, _utcNow());
            _localScan = null;
        }
    }

    private void CacheNetworkScan(Task<SqlServerInstanceDiscoveryInventory> completed)
    {
        var inventory = ReadCompletedScan(completed, "network");
        SqlServerInstanceDiscoveryResult? notification = null;
        lock (_gate)
        {
            if (!ReferenceEquals(_networkScan, completed))
                return;

            _cachedNetwork = new CachedInventory(inventory, _utcNow());
            _networkScan = null;

            if (_localScan is { IsCompleted: true } localScan)
            {
                _cachedLocal = new CachedInventory(ReadCompletedScan(localScan, "local"), _utcNow());
                _localScan = null;
            }

            if (_cachedLocal is not null)
            {
                notification = CreateResult(
                    _cachedLocal.Inventory,
                    inventory,
                    isTimedOut: false,
                    isFromCache: false,
                    networkScanStillRunning: false);
            }
        }

        NotifyNetworkResultsCompleted(notification);
    }

    private static SqlServerInstanceDiscoveryInventory ReadCompletedScan(
        Task<SqlServerInstanceDiscoveryInventory> completed,
        string inventoryName)
    {
        try { return completed.GetAwaiter().GetResult(); }
        catch (Exception exception)
        {
            Trace.TraceWarning("SQL Server {0} scan task failed ({1}).", inventoryName, exception.GetType().Name);
            return new SqlServerInstanceDiscoveryInventory(
                Array.Empty<string>(),
                IsComplete: false,
                FailureReason: exception.GetType().Name);
        }
    }

    private SqlServerInstanceDiscoveryInventory? GetLastKnownNetworkInventory()
    {
        lock (_gate)
            return _cachedNetwork?.Inventory;
    }

    private bool IsFresh(CachedInventory? cached, TimeSpan ttl)
    {
        if (cached is null)
            return false;
        var age = _utcNow() - cached.StoredAt;
        return age >= TimeSpan.Zero && age < ttl;
    }

    private TimeSpan GetTtl(SqlServerInstanceDiscoveryInventory? inventory, TimeSpan successTtl) =>
        inventory is { IsComplete: false } ? _failureCacheTtl : successTtl;

    private static SqlServerInstanceDiscoveryResult CreateResult(
        SqlServerInstanceDiscoveryInventory local,
        SqlServerInstanceDiscoveryInventory network,
        bool isTimedOut,
        bool isFromCache,
        bool networkScanStillRunning)
    {
        var instances = HiveSqlServerInstanceDiscovery.MergeCandidates(network.Instances, local.Instances);
        var complete = local.IsComplete && network.IsComplete && !isTimedOut;
        var status = isTimedOut
            ? "Discovery timed out; results may be incomplete. Refresh or enter Custom..."
            : !network.IsComplete
                ? "Network discovery was incomplete; available results are shown. Refresh or enter Custom..."
                : !local.IsComplete
                    ? "Local instance discovery was incomplete; available results are shown. Refresh or enter Custom..."
                    : instances.Count == 0
                        ? "No instances found. Refresh or enter Custom..."
                        : $"{instances.Count} SQL Server instance(s) found.";

        return new SqlServerInstanceDiscoveryResult(
            instances,
            complete,
            isTimedOut,
            isFromCache,
            networkScanStillRunning,
            status);
    }

    private void NotifyNetworkResultsCompleted(SqlServerInstanceDiscoveryResult? result)
    {
        if (result is null || NetworkResultsCompleted is not { } handlers)
            return;
        foreach (Action<SqlServerInstanceDiscoveryResult> handler in handlers.GetInvocationList())
        {
            try { handler(result); }
            catch (Exception exception)
            {
                Trace.TraceWarning("SQL Server late discovery subscriber failed ({0}).", exception.GetType().Name);
            }
        }
    }

    private static void ReportSafely(
        IProgress<SqlServerInstanceDiscoveryUpdate>? progress,
        SqlServerInstanceDiscoveryUpdate update)
    {
        if (progress is null)
            return;
        try { progress.Report(update); }
        catch (Exception exception)
        {
            Trace.TraceWarning("SQL Server discovery progress subscriber failed ({0}).", exception.GetType().Name);
        }
    }

    private sealed record CachedInventory(
        SqlServerInstanceDiscoveryInventory Inventory,
        DateTimeOffset StoredAt);
}

internal static class HiveSqlServerInstanceDiscovery
{
    internal const string SearchingStatusMessage = "Searching for SQL Server instances…";

    internal static SqlServerInstanceDiscoveryCoordinator Shared { get; } =
        new(
            DiscoverInstalledLocalInstances,
            DiscoverNetworkInstances,
            localCacheTtl: TimeSpan.FromMinutes(5),
            networkCacheTtl: TimeSpan.FromMinutes(1),
            failureCacheTtl: TimeSpan.FromSeconds(15),
            networkWaitTimeout: TimeSpan.FromSeconds(3));

    internal static IReadOnlyList<string> MergeCandidates(
        IEnumerable<string> networkInstances,
        IEnumerable<string> localInstances)
    {
        ArgumentNullException.ThrowIfNull(networkInstances);
        ArgumentNullException.ThrowIfNull(localInstances);
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var instance in networkInstances)
            if (!string.IsNullOrWhiteSpace(instance))
                names.Add(instance.Trim());
        foreach (var instance in localInstances)
            if (!string.IsNullOrWhiteSpace(instance))
                names.Add(FormatInstalledInstanceName(instance));
        return names.ToArray();
    }

    internal static string FormatInstalledInstanceName(string instanceName)
    {
        var normalized = instanceName.Trim();
        if (normalized.Length == 0)
            throw new ArgumentException("SQL Server instance name is required.", nameof(instanceName));
        return string.Equals(normalized, "MSSQLSERVER", StringComparison.OrdinalIgnoreCase)
            ? "localhost"
            : $@"localhost\{normalized}";
    }

    private static SqlServerInstanceDiscoveryInventory DiscoverNetworkInstances()
    {
        // Synchronous and not token-cancellable. One dedicated worker owns it, and
        // only the caller's wait is bounded.
        var table = SqlDataSourceEnumerator.Instance.GetDataSources();
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DataRow row in table.Rows)
        {
            var server = Convert.ToString(row["ServerName"])?.Trim();
            var instance = Convert.ToString(row["InstanceName"])?.Trim();
            if (string.IsNullOrWhiteSpace(server))
                continue;
            names.Add(string.IsNullOrWhiteSpace(instance) ? server : $@"{server}\{instance}");
        }
        return new SqlServerInstanceDiscoveryInventory(names.ToArray());
    }

    private static SqlServerInstanceDiscoveryInventory DiscoverInstalledLocalInstances()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var failures = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var instanceNames = baseKey.OpenSubKey(
                    @"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL");
                if (instanceNames is null)
                    continue;
                foreach (var name in instanceNames.GetValueNames())
                    if (!string.IsNullOrWhiteSpace(name))
                        names.Add(name.Trim());
            }
            catch (System.Security.SecurityException exception) { failures.Add(exception.GetType().Name); }
            catch (UnauthorizedAccessException exception) { failures.Add(exception.GetType().Name); }
            catch (IOException exception) { failures.Add(exception.GetType().Name); }
        }

        return new SqlServerInstanceDiscoveryInventory(
            names.ToArray(),
            IsComplete: failures.Count == 0,
            FailureReason: failures.Count == 0 ? null : string.Join(", ", failures));
    }
}
