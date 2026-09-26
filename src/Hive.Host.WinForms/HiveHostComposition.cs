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
        _status;

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

        current?.Dispose();
        _lifetimeCts.Dispose();

        // Do not synchronously wait on the async reconfiguration gate here.
        // An in-flight ComposeAsync operation will observe the lifetime
        // cancellation, unwind, and release the gate without blocking the
        // WinForms shutdown path.

        GC.SuppressFinalize(this);
    }

    private async Task<Result<HiveHostServiceGraph>> ComposeAsync(
        CancellationToken cancellationToken,
        bool skipWhenUnchanged = false)
    {
        ThrowIfDisposed();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            _lifetimeCts.Token);
        var operationToken = linkedCts.Token;

        await _reconfigurationGate
            .WaitAsync(operationToken)
            .ConfigureAwait(false);

        try
        {
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
                current.PersistenceConfiguration == configuration.Value)
            {
                operationToken.ThrowIfCancellationRequested();

                _status = new HiveHostCompositionStatus(
                    HiveHostCompositionState.Ready,
                    null);

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
                candidate.Value.Dispose();
                throw new OperationCanceledException(operationToken);
            }

            previous?.Dispose();

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
            _reconfigurationGate.Release();
        }
    }

    private void SetFailure(Error error)
    {
        var state = Volatile.Read(ref _current) is null
            ? HiveHostCompositionState.Unavailable
            : HiveHostCompositionState.ReplacementFailed;

        _status = new HiveHostCompositionStatus(
            state,
            error);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
