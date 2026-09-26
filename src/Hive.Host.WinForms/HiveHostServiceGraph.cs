using Hive.Management;

namespace Hive.Host.WinForms;

public sealed class HiveHostServiceGraph : IDisposable
{
    private readonly IReadOnlyList<IDisposable> _ownedResources;
    private int _disposed;

    public HiveHostServiceGraph(
        Hive.Core.HivePersistenceConfiguration persistenceConfiguration,
        IHiveManagementFacade management,
        IEnumerable<IDisposable>? ownedResources = null)
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
    }

    public Hive.Core.HivePersistenceConfiguration PersistenceConfiguration { get; }

    public IHiveManagementFacade Management { get; }

    public bool IsDisposed =>
        Volatile.Read(ref _disposed) != 0;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

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

        GC.SuppressFinalize(this);
    }
}
