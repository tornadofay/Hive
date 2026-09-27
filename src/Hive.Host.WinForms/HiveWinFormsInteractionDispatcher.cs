using Hive.Core;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

internal sealed class HiveWinFormsInteractionDispatcher
{
    private readonly Control _root;
    private readonly IHiveWinFormsSemanticProvider? _semanticProvider;
    private readonly HiveWinFormsTargetResolver _targetResolver;

    public HiveWinFormsInteractionDispatcher(
        Control root,
        IHiveWinFormsSemanticProvider? semanticProvider,
        HiveWinFormsTargetResolver targetResolver)
    {
        _root = root;
        _semanticProvider = semanticProvider;
        _targetResolver = targetResolver;
    }

    public async Task<Result<HiveHostInteractionResult>> ExecuteAsync(
        HiveHostInteractionRequest request,
        HiveWinFormsCapturedHostState? state,
        CancellationToken cancellationToken)
    {
        if (RequiresFreshCapture(request.Kind))
        {
            var captureValidation =
                _targetResolver.ValidateCaptureBinding(
                    request,
                    state,
                    cancellationToken);

            if (captureValidation is not null)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    captureValidation);
            }
        }

        if (request.Kind is
            HiveHostInteractionKind.ReadRow or
            HiveHostInteractionKind.AddRow or
            HiveHostInteractionKind.EditRow or
            HiveHostInteractionKind.DeleteRow or
            HiveHostInteractionKind.InvokeAction)
        {
            return await ExecuteWithProviderAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }

        if (_root.IsDisposed ||
            _root.Disposing)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.root-disposed",
                    "The registered WinForms host is no longer available."));
        }

        if (_root.InvokeRequired)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.ui-thread-required",
                    "WinForms host interaction must run on the UI thread."));
        }

        if (request.ControlId is null)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.control-required",
                    "A control identity is required for this interaction."));
        }

        var control = _targetResolver.FindControlById(
            request.ControlId,
            cancellationToken);

        if (control is null)
        {
            return Result<HiveHostInteractionResult>.Failure(
                new Error(
                    "hive.host.winforms.control-not-found",
                    ErrorCategory.NotFound,
                    "The requested WinForms control was not found."));
        }

        if (control.IsDisposed ||
            control.Disposing)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.control-disposed",
                    "The requested WinForms control is no longer available."));
        }

        if (control.InvokeRequired)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.ui-thread-required",
                    "WinForms host interaction must run on the UI thread."));
        }

        if (request.Kind is
            not HiveHostInteractionKind.ReadControl and
            not HiveHostInteractionKind.SetControlValue)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.interaction-unsupported",
                    "The requested WinForms interaction is not supported by the reusable standard-control adapter."));
        }

        if (request.Kind == HiveHostInteractionKind.ReadControl &&
            !WinFormsControlValueAdapters.TryGet(control, out _))
        {
            return Result<HiveHostInteractionResult>.Failure(
                new Error(
                    "hive.host.winforms.capability-not-found",
                    ErrorCategory.NotFound,
                    "The requested WinForms capability is not exposed for this control."));
        }

        if (request.Kind == HiveHostInteractionKind.SetControlValue &&
            !CanSetStandardValue(control))
        {
            return Result<HiveHostInteractionResult>.Failure(
                new Error(
                    "hive.host.winforms.capability-not-found",
                    ErrorCategory.NotFound,
                    "The requested WinForms control does not currently expose a writable value capability."));
        }

        var capabilitySuffix = request.Kind switch
        {
            HiveHostInteractionKind.ReadControl => "read",
            HiveHostInteractionKind.SetControlValue => "set",
            _ => throw new InvalidOperationException(
                "The host interaction kind is invalid.")
        };

        var controlIdentity = request.ControlId["control:".Length..];

        var expectedCapabilityId = HiveWinFormsCapabilityIdentity.CreateId(
            "control|" +
            controlIdentity +
            "|" +
            capabilitySuffix);

        if (expectedCapabilityId != request.CapabilityId)
        {
            return Result<HiveHostInteractionResult>.Failure(
                new Error(
                    "hive.host.winforms.capability-mismatch",
                    ErrorCategory.Forbidden,
                    "The requested capability is not authorized for the supplied WinForms target."));
        }

        try
        {
            return request.Kind switch
            {
                HiveHostInteractionKind.ReadControl =>
                    Result<HiveHostInteractionResult>.Success(
                        new HiveHostInteractionResult(
                            request.CorrelationId,
                            request.Kind,
                            WinFormsControlValueAdapters.TryReadValue(control))),

                HiveHostInteractionKind.SetControlValue =>
                    SetControlValue(control, request),

                _ => Result<HiveHostInteractionResult>.Failure(
                    Error.Unsupported(
                        "hive.host.winforms.interaction-unsupported",
                        "The requested WinForms interaction is not supported by the reusable standard-control adapter."))
            };
        }
        catch (InvalidOperationException)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.interaction-conflict",
                    "The requested standard-control interaction could not be completed."));
        }
    }

    private async Task<Result<HiveHostInteractionResult>> ExecuteWithProviderAsync(
        HiveHostInteractionRequest request,
        CancellationToken cancellationToken)
    {
        if (_semanticProvider is null)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.interaction-provider-unavailable",
                    "This host did not supply a semantic interaction provider for the requested operation."));
        }

        return await _semanticProvider
            .ExecuteInteractionAsync(request, cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool RequiresFreshCapture(
        HiveHostInteractionKind kind) =>
        kind is
            HiveHostInteractionKind.SetControlValue or
            HiveHostInteractionKind.AddRow or
            HiveHostInteractionKind.EditRow or
            HiveHostInteractionKind.DeleteRow or
            HiveHostInteractionKind.InvokeAction;

    private static bool CanSetStandardValue(Control control)
    {
        if (!control.Enabled ||
            !WinFormsControlValueAdapters.TryGet(
                control,
                out var adapter) ||
            !adapter.CanSet(control))
        {
            return false;
        }

        if (control is IHiveWinFormsFieldControl fieldControl)
        {
            var metadata = fieldControl.HiveField;

            if (metadata.ReadOnly == true ||
                metadata.Computed == true ||
                metadata.Generated == true ||
                metadata.IsPrimaryKey == true)
            {
                return false;
            }
        }

        return true;
    }

    private static Result<HiveHostInteractionResult> SetControlValue(
        Control control,
        HiveHostInteractionRequest request)
    {
        if (!control.Enabled)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.control-disabled",
                    "The requested WinForms control is disabled."));
        }

        if (control is IHiveWinFormsFieldControl fieldControl)
        {
            var metadata = fieldControl.HiveField;

            if (metadata.ReadOnly == true ||
                metadata.Computed == true ||
                metadata.Generated == true ||
                metadata.IsPrimaryKey == true)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Conflict(
                        "hive.host.winforms.field-write-blocked",
                        "The requested Hive field is not directly writable."));
            }
        }

        return WinFormsControlValueAdapters.SetControlValue(
            control,
            request);
    }
}
