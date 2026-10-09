using Hive.Core;

namespace Hive.Management;

/// <summary>
/// Coordinates ordinary Management operations with exclusive persistence migration
/// and graph-retirement leases.
/// </summary>
public sealed class HiveManagementOperationGate :
    IHiveManagementOperationGate,
    IHivePersistenceMigrationQuiescence
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _migrationGate = new(1, 1);

    private int _activeOperations;
    private bool _quiescing;
    private bool _retiring;
    private bool _closed;
    private TaskCompletionSource? _operationsDrained;

    public IAsyncDisposable? TryEnterOperation()
    {
        lock (_sync)
        {
            if (_quiescing || _retiring || _closed)
                return null;

            _activeOperations++;
            return new GateLease(ReleaseOperation);
        }
    }

    public async Task<Result<IAsyncDisposable>> AcquireAsync(
        CancellationToken cancellationToken = default)
    {
        await _migrationGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        var quiescenceStarted = false;
        try
        {
            Task? drainTask;
            lock (_sync)
            {
                if (_retiring || _closed)
                {
                    _migrationGate.Release();
                    return Result<IAsyncDisposable>.Failure(
                        Error.Conflict(
                            "hive.management.operation-gate-closed",
                            "The Hive management graph is being retired and no longer accepts persistence migrations."));
                }

                _quiescing = true;
                quiescenceStarted = true;

                if (_activeOperations == 0)
                {
                    drainTask = null;
                }
                else
                {
                    _operationsDrained ??= new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    drainTask = _operationsDrained.Task;
                }
            }

            if (drainTask is not null)
                await drainTask.WaitAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            return Result<IAsyncDisposable>.Success(
                new GateLease(ReleaseQuiescence));
        }
        catch
        {
            if (quiescenceStarted)
                ReleaseQuiescence();
            else
                _migrationGate.Release();

            throw;
        }
    }

    /// <summary>
    /// Permanently prevents new ordinary Management calls and queued migrations from
    /// starting. Online graph retirement calls this while holding the retirement lease;
    /// synchronous process shutdown may close admission before immediate disposal.
    /// </summary>
    public void CloseAdmission()
    {
        lock (_sync)
        {
            _retiring = true;
            _closed = true;
        }
    }

    /// <summary>
    /// Closes admission to this graph, waits for any active migration and ordinary
    /// operations to finish, and returns the exclusive lease for safe graph disposal.
    /// The retiring flag is published before waiting on the migration semaphore, so
    /// queued/new migrations are rejected instead of delaying retirement.
    /// </summary>
    public async Task<IAsyncDisposable> AcquireRetirementLeaseAsync()
    {
        lock (_sync)
        {
            _retiring = true;
        }

        await _migrationGate.WaitAsync().ConfigureAwait(false);

        var quiescenceStarted = false;
        try
        {
            Task? drainTask;
            lock (_sync)
            {
                _closed = true;
                _quiescing = true;
                quiescenceStarted = true;

                if (_activeOperations == 0)
                {
                    drainTask = null;
                }
                else
                {
                    _operationsDrained ??= new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    drainTask = _operationsDrained.Task;
                }
            }

            if (drainTask is not null)
                await drainTask.ConfigureAwait(false);

            return new GateLease(ReleaseQuiescence);
        }
        catch
        {
            if (quiescenceStarted)
                ReleaseQuiescence();
            else
                _migrationGate.Release();

            throw;
        }
    }

    private void ReleaseOperation()
    {
        lock (_sync)
        {
            if (_activeOperations <= 0)
                throw new InvalidOperationException(
                    "The Management operation gate released an operation more than once.");

            _activeOperations--;
            if (_quiescing && _activeOperations == 0)
                _operationsDrained?.TrySetResult();
        }
    }

    private void ReleaseQuiescence()
    {
        lock (_sync)
        {
            if (!_quiescing)
                return;

            _quiescing = false;
            _operationsDrained = null;
        }

        _migrationGate.Release();
    }

    private sealed class GateLease(Action release) : IAsyncDisposable
    {
        private Action? _release = release;

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _release, null)?.Invoke();
            return ValueTask.CompletedTask;
        }
    }
}
