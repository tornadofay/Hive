using System.Collections.Generic;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

public interface IHiveWinFormsSemanticProvider
{
    bool TryDescribeDataSurface(
        DataGridView grid,
        string surfaceId,
        out HiveHostDataSurfaceDescriptor descriptor);

    Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
        HiveHostInteractionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
        HiveLookupRequest request,
        CancellationToken cancellationToken = default);

    IReadOnlyList<HiveHostBusinessOperationDescriptor> GetBusinessOperations();
}

public sealed class HiveWinFormsHostIntegrationAdapter :
    IHiveHostIntegrationAdapter,
    IDisposable
{
    private readonly HiveWinFormsHostContext _context;
    private readonly HiveWinFormsHostRegistration _registration;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveWinFormsSemanticProvider? _semanticProvider;
    private readonly HiveWinFormsHostCaptureService _captureService;
    private readonly HiveWinFormsInteractionDispatcher _interactionDispatcher;
    private readonly object _captureGate = new();
    private HiveWinFormsCapturedHostState? _currentState;
    private long _captureGeneration;
    private int _disposed;

    public HiveWinFormsHostIntegrationAdapter(
        Form root,
        ResourceAccessContext accessContext,
        IHiveWinFormsSemanticProvider? semanticProvider = null,
        HiveWinFormsHostContextOptions? options = null,
        IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.DeploymentId is null ||
            accessContext.PrincipalId is null)
        {
            throw new ArgumentException(
                "WinForms host integration requires deployment and principal identity.",
                nameof(accessContext));
        }

        _accessContext = accessContext;
        _semanticProvider = semanticProvider;
        var effectiveOptions = options ?? new HiveWinFormsHostContextOptions();
        _context = new HiveWinFormsHostContext(
            accessContext,
            effectiveOptions,
            clock);
        _registration = _context.Register(root);

        var targetResolver = new HiveWinFormsTargetResolver(
            _registration.Root,
            effectiveOptions);

        _captureService = new HiveWinFormsHostCaptureService(
            _context,
            _registration,
            _accessContext,
            _semanticProvider,
            targetResolver,
            new HiveWinFormsDataSurfaceDescriptorBuilder());

        _interactionDispatcher = new HiveWinFormsInteractionDispatcher(
            _registration.Root,
            _semanticProvider,
            targetResolver);
    }

    public string AdapterId => "Hive.Host.WinForms";

    public async Task<Result<HiveHostContextDescriptor>> CaptureAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (!Equals(_accessContext, accessContext))
        {
            return Result<HiveHostContextDescriptor>.Failure(
                new Error(
                    "hive.host.winforms.access-context-mismatch",
                    ErrorCategory.Forbidden,
                    "The supplied host integration access context does not match the registered host."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var captureGeneration = Interlocked.Increment(ref _captureGeneration);

        if (_registration.Root.IsDisposed ||
            _registration.Root.Disposing)
        {
            return Result<HiveHostContextDescriptor>.Failure(
                Error.Conflict(
                    "hive.host.winforms.root-disposed",
                    "The registered WinForms host is no longer available."));
        }

        if (_registration.Root.InvokeRequired)
        {
            return Result<HiveHostContextDescriptor>.Failure(
                Error.Validation(
                    "hive.host.winforms.ui-thread-required",
                    "WinForms host capture must run on the UI thread."));
        }

        var capture = await _captureService
            .CaptureAsync(captureGeneration, cancellationToken)
            .ConfigureAwait(true);

        if (capture.IsFailure)
            return Result<HiveHostContextDescriptor>.Failure(capture.Error!);

        lock (_captureGate)
        {
            if (captureGeneration != _captureGeneration)
            {
                return Result<HiveHostContextDescriptor>.Failure(
                    Error.Conflict(
                        "hive.host.winforms.capture-superseded",
                        "The host capture was superseded by a newer capture."));
            }

            _currentState = capture.Value!;
            return Result<HiveHostContextDescriptor>.Success(
                capture.Value!.Capture);
        }
    }

    public async Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
        HiveHostInteractionRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

        if (!Equals(_accessContext, accessContext))
        {
            return Result<HiveHostInteractionResult>.Failure(
                new Error(
                    "hive.host.winforms.access-context-mismatch",
                    ErrorCategory.Forbidden,
                    "The supplied host integration access context does not match the registered host."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await _interactionDispatcher
            .ExecuteAsync(
                request,
                GetCurrentState(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
        HiveLookupRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(request);

        if (!Equals(_accessContext, accessContext))
        {
            return Result<IReadOnlyList<HiveLookupOption>>.Failure(
                new Error(
                    "hive.host.winforms.access-context-mismatch",
                    ErrorCategory.Forbidden,
                    "The supplied host integration access context does not match the registered host."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (_semanticProvider is null)
        {
            return Result<IReadOnlyList<HiveLookupOption>>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.lookup-provider-unavailable",
                    "This host did not supply a bounded lookup provider."));
        }

        return await _semanticProvider
            .ResolveLookupAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _registration.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }

    private HiveWinFormsCapturedHostState? GetCurrentState()
    {
        lock (_captureGate)
        {
            return _currentState;
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
