using System.Collections.Generic;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Host.WinForms;

internal sealed class HiveWinFormsHostCaptureService
{
    private readonly HiveWinFormsHostContext _context;
    private readonly HiveWinFormsHostRegistration _registration;
    private readonly ResourceAccessContext _accessContext;
    private readonly IHiveWinFormsSemanticProvider? _semanticProvider;
    private readonly HiveWinFormsTargetResolver _targetResolver;
    private readonly HiveWinFormsDataSurfaceDescriptorBuilder _dataSurfaceBuilder;

    public HiveWinFormsHostCaptureService(
        HiveWinFormsHostContext context,
        HiveWinFormsHostRegistration registration,
        ResourceAccessContext accessContext,
        IHiveWinFormsSemanticProvider? semanticProvider,
        HiveWinFormsTargetResolver targetResolver,
        HiveWinFormsDataSurfaceDescriptorBuilder dataSurfaceBuilder)
    {
        _context = context;
        _registration = registration;
        _accessContext = accessContext;
        _semanticProvider = semanticProvider;
        _targetResolver = targetResolver;
        _dataSurfaceBuilder = dataSurfaceBuilder;
    }

    public async Task<Result<HiveWinFormsCapturedHostState>> CaptureAsync(
        long generation,
        CancellationToken cancellationToken)
    {
        var snapshot = await _context
            .CaptureAsync(_registration, cancellationToken)
            .ConfigureAwait(true);

        if (snapshot.IsFailure)
        {
            return Result<HiveWinFormsCapturedHostState>.Failure(
                snapshot.Error!);
        }

        try
        {
            if (_registration.Root.IsDisposed ||
                _registration.Root.Disposing)
            {
                return Result<HiveWinFormsCapturedHostState>.Failure(
                    Error.Conflict(
                        "hive.host.winforms.root-disposed",
                        "The registered WinForms host is no longer available."));
            }

            if (_registration.Root.InvokeRequired)
            {
                return Result<HiveWinFormsCapturedHostState>.Failure(
                    Error.Validation(
                        "hive.host.winforms.ui-thread-required",
                        "WinForms host capture must run on the UI thread."));
            }

            var capturedControls =
                new List<(HiveWinFormsControlSnapshot Snapshot, Control Control)>(
                    snapshot.Value!.Controls.Count);

            foreach (var controlSnapshot in snapshot.Value.Controls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var control = _targetResolver.FindControlByPath(
                    controlSnapshot.Path,
                    cancellationToken);

                if (control is null)
                {
                    return Result<HiveWinFormsCapturedHostState>.Failure(
                        new Error(
                            "hive.host.winforms.control-not-found",
                            ErrorCategory.NotFound,
                            "A control discovered during host capture is no longer available."));
                }

                capturedControls.Add((controlSnapshot, control));
            }

            var controlIds = CreateControlIdentityMap(capturedControls);
            var surfaceEntries =
                new List<HiveWinFormsDataSurfaceCaptureEntry>();

            var controls = new List<HiveHostControlDescriptor>(
                capturedControls.Count);

            foreach (var entry in capturedControls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var controlId = controlIds[entry.Control];
                var fieldMetadata = entry.Control is IHiveWinFormsFieldControl fieldControl
                    ? fieldControl.HiveField
                    : null;

                controls.Add(
                    CreateControlDescriptor(
                        entry.Snapshot,
                        entry.Control,
                        controlId,
                        fieldMetadata));

                if (entry.Control is not DataGridView grid)
                    continue;

                var surfaceId = CreateDataSurfaceIdentity(
                    grid,
                    entry.Snapshot,
                    capturedControls);

                HiveHostDataSurfaceDescriptor surface;
                HiveWinFormsDataSurfaceMetadata? metadata = null;

                if (grid is IHiveWinFormsDataSurface hiveSurface)
                {
                    metadata = hiveSurface.HiveDataSurface;
                    surface = _dataSurfaceBuilder.Create(
                        grid,
                        surfaceId,
                        metadata);
                }
                else if (_semanticProvider is not null &&
                         _semanticProvider.TryDescribeDataSurface(
                             grid,
                             surfaceId,
                             out var semanticSurface))
                {
                    surface = semanticSurface;
                }
                else
                {
                    surface = _dataSurfaceBuilder.Create(
                        grid,
                        surfaceId,
                        null);
                }

                surfaceEntries.Add(
                    new HiveWinFormsDataSurfaceCaptureEntry(
                        grid,
                        metadata,
                        surface,
                        entry.Snapshot.Path));
            }

            var dataSurfaces =
                HiveWinFormsDataSurfaceDescriptorBuilder.ApplyParentChildRelationships(
                    surfaceEntries);

            var descriptor = new HiveHostContextDescriptor(
                snapshot.Value.Provenance.RegistrationId,
                GetHostName(
                    snapshot.Value.RootName,
                    snapshot.Value.RootRuntimeType),
                new HiveHostProvenance(
                    snapshot.Value.Provenance.RegistrationId,
                    snapshot.Value.Provenance.CaptureId,
                    snapshot.Value.Provenance.CapturedAtUtc,
                    CorrelationId.New(),
                    "Hive.Host.WinForms",
                    _accessContext),
                controls,
                dataSurfaces,
                _semanticProvider?.GetBusinessOperations()
                    ?? Array.Empty<HiveHostBusinessOperationDescriptor>());

            var controlsById = capturedControls.ToDictionary(
                entry => "control:" + controlIds[entry.Control],
                entry => entry.Control,
                StringComparer.Ordinal);

            var surfacesById = surfaceEntries.ToDictionary(
                entry => entry.Surface.Id,
                entry => entry.Grid,
                StringComparer.Ordinal);

            var surfacePathsById = surfaceEntries.ToDictionary(
                entry => entry.Surface.Id,
                entry => entry.Path,
                StringComparer.Ordinal);

            var targetAncestryById =
                new Dictionary<string, IReadOnlyList<Control>>(
                    StringComparer.Ordinal);

            foreach (var entry in capturedControls)
            {
                var controlId = controlIds[entry.Control];
                targetAncestryById["control:" + controlId] =
                    HiveWinFormsTargetResolver.CaptureControlAncestry(
                        entry.Control);
            }

            foreach (var entry in surfaceEntries)
            {
                targetAncestryById[entry.Surface.Id] =
                    HiveWinFormsTargetResolver.CaptureControlAncestry(
                        entry.Grid);
            }

            return Result<HiveWinFormsCapturedHostState>.Success(
                new HiveWinFormsCapturedHostState(
                    generation,
                    descriptor,
                    controlsById,
                    surfacesById,
                    surfacePathsById,
                    targetAncestryById));
        }
        catch (HiveWinFormsIntegrationException exception)
        {
            return Result<HiveWinFormsCapturedHostState>.Failure(
                new Error(
                    exception.Code,
                    ErrorCategory.Validation,
                    "The WinForms host-context snapshot could not be captured."));
        }
        catch (InvalidOperationException)
        {
            return Result<HiveWinFormsCapturedHostState>.Failure(
                new Error(
                    "hive.host.winforms.capture-failed",
                    ErrorCategory.Conflict,
                    "WinForms host integration discovery could not be completed."));
        }
    }

    private static HiveHostControlDescriptor CreateControlDescriptor(
        HiveWinFormsControlSnapshot snapshot,
        Control control,
        string controlId,
        HiveWinFormsFieldMetadata? fieldMetadata)
    {
        var field = CreateStandardField(
            control,
            snapshot,
            fieldMetadata);

        var capabilities = new List<HiveHostCapabilityDescriptor>();

        if (field is not null)
        {
            capabilities.Add(
                HiveWinFormsCapabilityIdentity.Create(
                    "control|" + controlId + "|read",
                    HiveHostCapabilityKind.ReadControl,
                    "Read control value"));
        }

        if (CanSetStandardValue(control, field))
        {
            capabilities.Add(
                HiveWinFormsCapabilityIdentity.Create(
                    "control|" + controlId + "|set",
                    HiveHostCapabilityKind.SetControlValue,
                    "Set control value"));
        }

        return new HiveHostControlDescriptor(
            "control:" + controlId,
            snapshot.Path,
            snapshot.Depth,
            snapshot.RuntimeType,
            snapshot.Name,
            snapshot.AccessibleName,
            snapshot.Visible,
            snapshot.Enabled,
            snapshot.Focused,
            field?.ReadOnly ?? snapshot.ReadOnly,
            field,
            capabilities);
    }

    private static HiveHostFieldDescriptor? CreateStandardField(
        Control control,
        HiveWinFormsControlSnapshot snapshot,
        HiveWinFormsFieldMetadata? metadata)
    {
        if (!WinFormsControlValueAdapters.TryGet(control, out _))
            return null;

        var bindingMember =
            HiveWinFormsText.CleanOptional(metadata?.BindingMember) ??
            snapshot.Bindings
                .Select(static binding => binding.BindingMember)
                .FirstOrDefault(static member => !string.IsNullOrWhiteSpace(member));

        var fieldName = HiveWinFormsText.CleanRequired(
            metadata?.Name,
            bindingMember ??
            snapshot.Name ??
            snapshot.Path);

        var readOnly =
            snapshot.ReadOnly ||
            metadata?.ReadOnly == true;

        return new HiveHostFieldDescriptor(
            fieldName,
            HiveWinFormsText.CleanOptional(metadata?.BindingMember) ?? bindingMember,
            HiveWinFormsText.CleanRequired(
                metadata?.ValueType,
                WinFormsControlValueAdapters.GetValueTypeName(control)),
            metadata?.Required ?? false,
            readOnly,
            metadata?.Computed ?? false,
            metadata?.Generated ?? false,
            metadata?.IsPrimaryKey ?? false,
            WinFormsControlValueAdapters.TryReadValue(control),
            metadata?.Lookup);
    }

    private static bool CanSetStandardValue(
        Control control,
        HiveHostFieldDescriptor? field = null)
    {
        if (!control.Enabled ||
            !WinFormsControlValueAdapters.TryGet(
                control,
                out var adapter) ||
            !adapter.CanSet(control))
        {
            return false;
        }

        if (field is not null &&
            (field.ReadOnly ||
             field.Computed ||
             field.Generated ||
             field.IsPrimaryKey))
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

    private static Dictionary<Control, string> CreateControlIdentityMap(
        IReadOnlyList<(HiveWinFormsControlSnapshot Snapshot, Control Control)> controls)
    {
        var nameCounts = controls
            .Where(static entry => !string.IsNullOrWhiteSpace(entry.Snapshot.Name))
            .GroupBy(
                static entry => entry.Snapshot.Name!,
                StringComparer.Ordinal)
            .ToDictionary(
                static group => group.Key,
                static group => group.Count(),
                StringComparer.Ordinal);

        var candidates = new Dictionary<Control, string>(
            ReferenceEqualityComparer.Instance);

        foreach (var entry in controls)
        {
            var explicitId = GetExplicitControlId(entry.Control);
            var candidate =
                explicitId ??
                (!string.IsNullOrWhiteSpace(entry.Snapshot.Name) &&
                 nameCounts[entry.Snapshot.Name!] == 1
                    ? entry.Snapshot.Name!.Trim()
                    : entry.Snapshot.Path);

            candidates.Add(
                entry.Control,
                "control:" + candidate);
        }

        var duplicates = candidates
            .GroupBy(
                static pair => pair.Value,
                StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicates is not null)
        {
            throw new HiveWinFormsIntegrationException(
                "hive.host.winforms.control-identity-duplicate",
                $"Control identity '{duplicates.Key}' is not unique within the captured host.");
        }

        var result = new Dictionary<Control, string>(
            ReferenceEqualityComparer.Instance);

        foreach (var candidate in candidates)
        {
            result.Add(
                candidate.Key,
                candidate.Value["control:".Length..]);
        }

        return result;
    }

    private static string CreateDataSurfaceIdentity(
        DataGridView grid,
        HiveWinFormsControlSnapshot snapshot,
        IReadOnlyList<(HiveWinFormsControlSnapshot Snapshot, Control Control)> controls)
    {
        var metadata = grid is IHiveWinFormsDataSurface hiveSurface
            ? hiveSurface.HiveDataSurface
            : null;

        var explicitId = HiveWinFormsText.CleanOptional(metadata?.SurfaceId);
        if (explicitId is not null)
            return "surface:" + explicitId;

        var nameCount = controls.Count(entry =>
            entry.Control is DataGridView &&
            string.Equals(
                entry.Snapshot.Name,
                snapshot.Name,
                StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(entry.Snapshot.Name));

        var candidate =
            !string.IsNullOrWhiteSpace(snapshot.Name) && nameCount == 1
                ? snapshot.Name.Trim()
                : snapshot.Path;

        return "surface:" + candidate;
    }

    private string GetHostName(
        string? rootName,
        string rootRuntimeType)
    {
        if (_registration.Root is HiveForm hiveForm)
        {
            var overrideName = HiveWinFormsText.CleanOptional(
                hiveForm.HiveHostIntegration.HostName);

            if (overrideName is not null)
                return overrideName;
        }

        return rootName ?? rootRuntimeType;
    }

    private static string? GetExplicitControlId(Control control) =>
        control is IHiveWinFormsControl hiveControl
            ? HiveWinFormsText.CleanOptional(
                hiveControl.HiveIntegration.ControlId)
            : null;
}
