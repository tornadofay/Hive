using Hive.Management;

namespace Hive.Host.WinForms;

/// <summary>
/// Owns one published persistence-backed Management graph.
/// Use asynchronous disposal when retiring a live graph so admitted operations can drain first.
/// </summary>
public sealed class HiveHostServiceGraph : IDisposable, IAsyncDisposable
{
    private readonly IReadOnlyList<IDisposable> _ownedResources;
    private readonly HiveManagementOperationGate? _operationGate;
    private int _disposed;

    public HiveHostServiceGraph(
        Hive.Core.HivePersistenceConfiguration persistenceConfiguration,
        IHiveManagementFacade management,
        IEnumerable<IDisposable>? ownedResources = null)
        : this(persistenceConfiguration, management, ownedResources, null)
    {
    }

    internal HiveHostServiceGraph(
        Hive.Core.HivePersistenceConfiguration persistenceConfiguration,
        IHiveManagementFacade management,
        IEnumerable<IDisposable>? ownedResources,
        HiveManagementOperationGate? operationGate)
    {
        PersistenceConfiguration = persistenceConfiguration
            ?? throw new ArgumentNullException(nameof(persistenceConfiguration));
        Management = management
            ?? throw new ArgumentNullException(nameof(management));

        _ownedResources = (ownedResources ?? Array.Empty<IDisposable>())
            .ToArray();

        if (_ownedResources.Any(static resource => resource is null))
        {
            throw new ArgumentException(
                "Owned resources cannot contain null values.",
                nameof(ownedResources));
        }

        _operationGate = operationGate;
    }

    public Hive.Core.HivePersistenceConfiguration PersistenceConfiguration { get; }

    public IHiveManagementFacade Management { get; }

    public bool IsDisposed =>
        Volatile.Read(ref _disposed) != 0;

    /// <summary>
    /// Closes operation admission and disposes immediately without waiting for active operations.
    /// This path is intended for synchronous host shutdown. Use DisposeAsync for online replacement.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _operationGate?.CloseAdmission();

        try
        {
            DisposeOwnedResources();
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Closes the retired graph to new work, waits for its active migration and admitted
    /// Management calls to finish, and then releases the owned resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        IAsyncDisposable? retirementLease = null;

        try
        {
            if (_operationGate is not null)
            {
                retirementLease = await _operationGate
                    .AcquireRetirementLeaseAsync()
                    .ConfigureAwait(false);
            }

            DisposeOwnedResources();
        }
        finally
        {
            if (retirementLease is not null)
                await retirementLease.DisposeAsync().ConfigureAwait(false);

            GC.SuppressFinalize(this);
        }
    }

    private void DisposeOwnedResources()
    {
        List<Exception>? failures = null;

        for (var index = _ownedResources.Count - 1; index >= 0; index--)
        {
            try
            {
                _ownedResources[index].Dispose();
            }
            catch (Exception exception)
            {
                failures ??= [];
                failures.Add(exception);
            }
        }

        if (failures is { Count: 1 })
            throw failures[0];

        if (failures is { Count: > 1 })
        {
            throw new AggregateException(
                "One or more Hive host graph resources failed to dispose.",
                failures);
        }
    }
}
