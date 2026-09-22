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

    private HiveHostServiceGraph? _current;
    private HiveHostCompositionStatus _status =
        new(HiveHostCompositionState.NotInitialized, null);
    private int _disposed;

    public HiveHostComposition()
    {
        var configurationStore = new JsonHiveConfigurationStore();

        _configurationStore = configurationStore;
        _graphFactory = new SqlHiveHostServiceGraphFactory(
            new UnavailableHiveBootstrapCredentialStore(),
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

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _reconfigurationGate.Wait();

        try
        {
            var current = Interlocked.Exchange(
                ref _current,
                null);

            current?.Dispose();

            _status = new HiveHostCompositionStatus(
                HiveHostCompositionState.Disposed,
                null);
        }
        finally
        {
            _reconfigurationGate.Release();
            _reconfigurationGate.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private async Task<Result<HiveHostServiceGraph>> ComposeAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        await _reconfigurationGate
            .WaitAsync(cancellationToken)
            .ConfigureAwait(false);

        try
        {
            var configuration = await _configurationStore
                .LoadPersistenceConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);

            if (configuration.IsFailure)
            {
                SetFailure(configuration.Error!);

                return Result<HiveHostServiceGraph>.Failure(
                    configuration.Error!);
            }

            var candidate = await _graphFactory
                .CreateAsync(
                    configuration.Value!,
                    cancellationToken)
                .ConfigureAwait(false);

            if (candidate.IsFailure)
            {
                SetFailure(candidate.Error!);

                return Result<HiveHostServiceGraph>.Failure(
                    candidate.Error!);
            }

            var previous = Interlocked.Exchange(
                ref _current,
                candidate.Value);

            _status = new HiveHostCompositionStatus(
                HiveHostCompositionState.Ready,
                null);

            previous?.Dispose();

            return candidate;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var error = new Error(
                "hive.host.composition-failed",
                ErrorCategory.External,
                $"Hive host composition failed: {exception.Message}");

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
