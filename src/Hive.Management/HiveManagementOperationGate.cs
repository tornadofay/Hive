using Hive.Core;

namespace Hive.Management;

/// <summary>
/// Drains in-flight Management facade operations and rejects new operations
/// while a persistence migration owns the exclusive lease.
/// </summary>
public sealed class HiveManagementOperationGate :
    IHiveManagementOperationGate,
    IHivePersistenceMigrationQuiescence
{
    private readonly object _sync = new();
    private readonly SemaphoreSlim _migrationGate = new(1, 1);

    private int _activeOperations;
    private bool _quiescing;
    private TaskCompletionSource? _operationsDrained;

    public IAsyncDisposable? TryEnterOperation()
    {
        lock (_sync)
        {
            if (_quiescing)
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
