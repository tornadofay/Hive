using System.Windows.Forms;
using Hive.Core;
using Hive.Management;

namespace Hive.Host.WinForms;

public enum HiveHostCompositionState
{
    NotInitialized,
    Ready,
    Unavailable,
    ReplacementFailed,
    Disposed
}

public sealed record HiveHostCompositionStatus(
    HiveHostCompositionState State,
    Error? LastError);

public sealed class HiveHostComposition : IDisposable
{
    private readonly IHiveConfigurationStore _configurationStore;
    private readonly IHiveHostServiceGraphFactory _graphFactory;
    private readonly SemaphoreSlim _reconfigurationGate = new(1, 1);
    private readonly CancellationTokenSource _lifetimeCts = new();
    private readonly object _stateGate = new();
    private int _activeCompositionOperations;
    private bool _reconfigurationGateDisposed;

    private HiveHostServiceGraph? _current;
    private HiveHostCompositionStatus _status =
        new(HiveHostCompositionState.NotInitialized, null);
    private int _disposed;

    public HiveHostComposition()
    {
        var configurationStore = new JsonHiveConfigurationStore(
            applicationName: Application.ProductName);

        _configurationStore = configurationStore;
        _graphFactory = new SqlHiveHostServiceGraphFactory(
            new DpapiHiveBootstrapCredentialStore(),
            configurationStore);
    }

    public HiveHostComposition(
        IHiveConfigurationStore configurationStore,
        IHiveHostServiceGraphFactory graphFactory)
    {
        _configurationStore = configurationStore
            ?? throw new ArgumentNullException(nameof(configurationStore));
        _graphFactory = graphFactory
            ?? throw new ArgumentNullException(nameof(graphFactory));
    }

    public HiveHostServiceGraph? Current =>
        Volatile.Read(ref _current);

    public HiveHostCompositionStatus Status =>
        Volatile.Read(ref _status);

    public Task<Result<HiveHostServiceGraph>> InitializeAsync(
        CancellationToken cancellationToken = default) =>
        ComposeAsync(cancellationToken);

    public Task<Result<HiveHostServiceGraph>> ReloadAsync(
        CancellationToken cancellationToken = default) =>
        ComposeAsync(cancellationToken);

    public Task<Result<HiveHostServiceGraph>> ApplyPersistedConfigurationAsync(
        CancellationToken cancellationToken = default) =>
        ComposeAsync(
            cancellationToken,
            skipWhenUnchanged: true);

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _lifetimeCts.Cancel();

        HiveHostServiceGraph? current;
        lock (_stateGate)
        {
            current = Interlocked.Exchange(
                ref _current,
                null);

            _status = new HiveHostCompositionStatus(
                HiveHostCompositionState.Disposed,
                null);
        }

        try
        {
            current?.Dispose();
        }
        finally
        {
            _lifetimeCts.Dispose();
            TryDisposeReconfigurationGateIfIdle();
        }

        // Do not synchronously wait on the async reconfiguration gate here.
        // An in-flight ComposeAsync operation will observe the lifetime
        // cancellation, unwind, release the gate, and dispose it when the
        // final composition operation exits without blocking the WinForms
        // shutdown path.

        GC.SuppressFinalize(this);
    }

    private async Task<Result<HiveHostServiceGraph>> ComposeAsync(
        CancellationToken cancellationToken,
        bool skipWhenUnchanged = false)
    {
        EnterCompositionOperation();

        CancellationTokenSource? linkedCts = null;
        var gateAcquired = false;

        try
        {
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                _lifetimeCts.Token);
            var operationToken = linkedCts.Token;

            await _reconfigurationGate
                .WaitAsync(operationToken)
                .ConfigureAwait(false);
            gateAcquired = true;

            operationToken.ThrowIfCancellationRequested();

            var configuration = await _configurationStore
                .LoadPersistenceConfigurationAsync(operationToken)
                .ConfigureAwait(false);

            if (configuration.IsFailure)
            {
                SetFailure(configuration.Error!);

                return Result<HiveHostServiceGraph>.Failure(
                    configuration.Error!);
            }

            var current = Volatile.Read(ref _current);

            if (skipWhenUnchanged &&
                current is not null &&
                current.PersistenceConfiguration == configuration.Value &&
                configuration.Value.AuthenticationMode != HiveSqlAuthenticationMode.SqlPassword)
            {
                operationToken.ThrowIfCancellationRequested();

                lock (_stateGate)
                {
                    if (Volatile.Read(ref _disposed) != 0)
                        throw new OperationCanceledException(operationToken);

                    _status = new HiveHostCompositionStatus(
                        HiveHostCompositionState.Ready,
                        null);
                }

                return Result<HiveHostServiceGraph>.Success(current);
            }

            var candidate = await _graphFactory
                .CreateAsync(
                    configuration.Value!,
                    operationToken)
                .ConfigureAwait(false);

            if (operationToken.IsCancellationRequested)
            {
                if (candidate.IsSuccess)
                    candidate.Value!.Dispose();

                operationToken.ThrowIfCancellationRequested();
            }

            if (candidate.IsFailure)
            {
                SetFailure(candidate.Error!);

                return Result<HiveHostServiceGraph>.Failure(
                    candidate.Error!);
            }

            HiveHostServiceGraph? previous = null;
            var publishCandidate = true;

            lock (_stateGate)
            {
                if (Volatile.Read(ref _disposed) != 0)
                {
                    publishCandidate = false;
                }
                else
                {
                    previous = Interlocked.Exchange(
                        ref _current,
                        candidate.Value);

                    _status = new HiveHostCompositionStatus(
                        HiveHostCompositionState.Ready,
                        null);
                }
            }

            if (!publishCandidate)
            {
                candidate.Value!.Dispose();
                throw new OperationCanceledException(operationToken);
            }

            if (previous is not null)
            {
                try
                {
                    previous.Dispose();
                }
                catch (Exception)
                {
                    var warning = new Error(
                        "hive.host.previous-graph-dispose-failed",
                        ErrorCategory.Internal,
                        "The previous Hive host graph could not be fully disposed.");

                    SetReadyWarning(warning);
                }
            }

            // Disposal can be triggered by disposal of a previous graph resource.
            // Treat that as a failed handoff rather than returning a graph that
            // has already lost its composition ownership.
            if (Volatile.Read(ref _disposed) != 0)
            {
                candidate.Value!.Dispose();
                throw new OperationCanceledException(operationToken);
            }

            return candidate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var error = new Error(
                "hive.host.composition-failed",
                ErrorCategory.External,
                "Hive host composition failed.");

            SetFailure(error);

            return Result<HiveHostServiceGraph>.Failure(error);
        }
        finally
        {
            if (gateAcquired)
                _reconfigurationGate.Release();

            linkedCts?.Dispose();
            ExitCompositionOperation();
        }
    }

    private void EnterCompositionOperation()
    {
        lock (_stateGate)
        {
            if (_disposed != 0)
                ObjectDisposedException.ThrowIf(
                    true,
                    this);

            _activeCompositionOperations++;
        }
    }

    private void ExitCompositionOperation()
    {
        var dispose = false;

        lock (_stateGate)
        {
            if (_activeCompositionOperations > 0)
                _activeCompositionOperations--;

            if (_disposed != 0 &&
                _activeCompositionOperations == 0 &&
                !_reconfigurationGateDisposed)
            {
                _reconfigurationGateDisposed = true;
                dispose = true;
            }
        }

        if (dispose)
            _reconfigurationGate.Dispose();
    }

    private void TryDisposeReconfigurationGateIfIdle()
    {
        var dispose = false;

        lock (_stateGate)
        {
            if (_disposed != 0 &&
                _activeCompositionOperations == 0 &&
                !_reconfigurationGateDisposed)
            {
                _reconfigurationGateDisposed = true;
                dispose = true;
            }
        }

        if (dispose)
            _reconfigurationGate.Dispose();
    }

    private void SetReadyWarning(Error error)
    {
        lock (_stateGate)
        {
            if (_disposed != 0)
                return;

            _status = new HiveHostCompositionStatus(
                HiveHostCompositionState.Ready,
                error);
        }
    }

    private void SetFailure(Error error)
    {
        lock (_stateGate)
        {
            if (Volatile.Read(ref _disposed) != 0)
                return;

            var state = Volatile.Read(ref _current) is null
                ? HiveHostCompositionState.Unavailable
                : HiveHostCompositionState.ReplacementFailed;

            _status = new HiveHostCompositionStatus(
                state,
                error);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
