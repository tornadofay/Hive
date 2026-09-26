using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
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
    private readonly HiveWinFormsHostContextOptions _options;
    private int _disposed;

    public HiveWinFormsHostIntegrationAdapter(
        Form root,
        ResourceAccessContext accessContext,
        IHiveWinFormsSemanticProvider? semanticProvider = null,
        HiveWinFormsHostContextOptions? options = null)
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
        _options = options ?? new HiveWinFormsHostContextOptions();
        _context = new HiveWinFormsHostContext(accessContext, _options);
        _registration = _context.Register(root);
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

        var snapshot = await _context
            .CaptureAsync(_registration, cancellationToken)
            .ConfigureAwait(true);

        if (snapshot.IsFailure)
            return Result<HiveHostContextDescriptor>.Failure(snapshot.Error!);

        try
        {
            if (_registration.Root.IsDisposed ||
                _registration.Root.Disposing ||
                _registration.Root.InvokeRequired)
            {
                return Result<HiveHostContextDescriptor>.Failure(
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

                var control = FindControl(controlSnapshot.Path);
                if (control is null)
                {
                    return Result<HiveHostContextDescriptor>.Failure(
                        new Error(
                            "hive.host.winforms.control-not-found",
                            ErrorCategory.NotFound,
                            "A control discovered during host capture is no longer available."));
                }

                capturedControls.Add((controlSnapshot, control));
            }

            var controlIds = CreateControlIdentityMap(capturedControls);
            var surfaceEntries =
                new List<(DataGridView Grid, HiveWinFormsDataSurfaceMetadata? Metadata, HiveHostDataSurfaceDescriptor Surface)>();

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
                    surface = CreateDataSurface(grid, surfaceId, metadata);
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
                    surface = CreateDataSurface(grid, surfaceId, null);
                }

                surfaceEntries.Add((grid, metadata, surface));
            }

            var dataSurfaces = ApplyParentChildRelationships(surfaceEntries);

            return Result<HiveHostContextDescriptor>.Success(
                new HiveHostContextDescriptor(
                    snapshot.Value.Provenance.RegistrationId,
                    GetHostName(
                        snapshot.Value.RootName,
                        snapshot.Value.RootRuntimeType),
                    new HiveHostProvenance(
                        snapshot.Value.Provenance.RegistrationId,
                        snapshot.Value.Provenance.CaptureId,
                        snapshot.Value.Provenance.CapturedAtUtc,
                        CorrelationId.New(),
                        AdapterId,
                        accessContext),
                    controls,
                    dataSurfaces,
                    _semanticProvider?.GetBusinessOperations()
                        ?? Array.Empty<HiveHostBusinessOperationDescriptor>()));
        }
        catch (HiveWinFormsIntegrationException exception)
        {
            return Result<HiveHostContextDescriptor>.Failure(
                new Error(
                    exception.Code,
                    ErrorCategory.Validation,
                    exception.Message));
        }
        catch (InvalidOperationException exception)
        {
            return Result<HiveHostContextDescriptor>.Failure(
                new Error(
                    "hive.host.winforms.capture-failed",
                    ErrorCategory.Conflict,
                    $"WinForms host integration discovery could not be completed: {exception.Message}"));
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

        if (_registration.Root.IsDisposed ||
            _registration.Root.Disposing)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.root-disposed",
                    "The registered WinForms host is no longer available."));
        }

        if (_registration.Root.InvokeRequired)
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

        var control = FindControlById(request.ControlId, cancellationToken);
        if (control is null)
        {
            return Result<HiveHostInteractionResult>.Failure(
                new Error(
                    "hive.host.winforms.control-not-found",
                    ErrorCategory.NotFound,
                    "The requested WinForms control was not found."));
        }

        if (control.IsDisposed || control.Disposing)
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
            !SupportsStandardField(control))
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
            _ => throw new InvalidOperationException("The host interaction kind is invalid.")
        };

        var controlIdentity = request.ControlId["control:".Length..];

        var expectedCapabilityId = CreateCapabilityId(
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
                            TryReadValue(control))),

                HiveHostInteractionKind.SetControlValue =>
                    SetControlValue(control, request),

                _ => Result<HiveHostInteractionResult>.Failure(
                    Error.Unsupported(
                        "hive.host.winforms.interaction-unsupported",
                        "The requested WinForms interaction is not supported by the reusable standard-control adapter."))
            };
        }
        catch (InvalidOperationException exception)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.interaction-conflict",
                    exception.Message));
        }
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

    private HiveHostControlDescriptor CreateControlDescriptor(
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
                CreateCapability(
                    "control|" + controlId + "|read",
                    HiveHostCapabilityKind.ReadControl,
                    "Read control value"));
        }

        if (CanSetStandardValue(control, field))
        {
            capabilities.Add(
                CreateCapability(
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
        if (!SupportsStandardField(control))
            return null;

        var bindingMember =
            CleanOptional(metadata?.BindingMember) ??
            snapshot.Bindings
                .Select(static binding => binding.BindingMember)
                .FirstOrDefault(static member => !string.IsNullOrWhiteSpace(member));

        var fieldName = CleanRequired(
            metadata?.Name,
            bindingMember ??
            snapshot.Name ??
            snapshot.Path);

        var readOnly =
            snapshot.ReadOnly ||
            metadata?.ReadOnly == true;

        return new HiveHostFieldDescriptor(
            fieldName,
            CleanOptional(metadata?.BindingMember) ?? bindingMember,
            CleanRequired(
                metadata?.ValueType,
                GetValueTypeName(control)),
            metadata?.Required ?? false,
            readOnly,
            metadata?.Computed ?? false,
            metadata?.Generated ?? false,
            metadata?.IsPrimaryKey ?? false,
            TryReadValue(control),
            metadata?.Lookup);
    }

    private static HiveHostDataSurfaceDescriptor CreateDataSurface(
        DataGridView grid,
        string surfaceId,
        HiveWinFormsDataSurfaceMetadata? metadata)
    {
        var fields = CreateDataSurfaceFields(grid, metadata);

        if (metadata is not null &&
            !string.IsNullOrWhiteSpace(metadata.PrimaryKeyField))
        {
            var primaryKey = metadata.PrimaryKeyField.Trim();
            var primaryIndex = fields.FindIndex(field =>
                string.Equals(field.Name, primaryKey, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(field.BindingMember, primaryKey, StringComparison.OrdinalIgnoreCase));

            if (primaryIndex < 0)
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.primary-key-field-not-found",
                    $"The configured primary-key field '{primaryKey}' is not present on data surface '{surfaceId}'.");
            }

            var existing = fields[primaryIndex];
            if (!existing.IsPrimaryKey)
            {
                fields[primaryIndex] = new HiveHostFieldDescriptor(
                    existing.Name,
                    existing.BindingMember,
                    existing.ValueType,
                    existing.Required,
                    true,
                    existing.Computed,
                    existing.Generated,
                    true,
                    existing.CurrentValue,
                    existing.Lookup);
            }
        }

        var capabilities = new List<HiveHostCapabilityDescriptor>
        {
            CreateCapability(
                "surface|" + surfaceId + "|read",
                HiveHostCapabilityKind.ReadDataSurface,
                "Read data surface")
        };

        if (metadata is not null)
        {
            foreach (var capability in metadata.Capabilities)
            {
                if (capabilities.Any(existing => existing.Id == capability.Id))
                {
                    throw new HiveWinFormsIntegrationException(
                        "hive.host.winforms.capability-duplicate",
                        $"Capability '{capability.Id}' is duplicated on data surface '{surfaceId}'.");
                }

                capabilities.Add(capability);
            }
        }

        return new HiveHostDataSurfaceDescriptor(
            surfaceId,
            CleanRequired(
                metadata?.Name,
                string.IsNullOrWhiteSpace(grid.Name)
                    ? "WinForms data surface"
                    : grid.Name),
            TryGetBoundRowCount(grid),
            fields,
            capabilities);
    }

    private static List<HiveHostFieldDescriptor> CreateDataSurfaceFields(
        DataGridView grid,
        HiveWinFormsDataSurfaceMetadata? metadata)
    {
        var fields = new List<HiveHostFieldDescriptor>();

        if (grid.Columns.Count > 0 || !grid.AutoGenerateColumns)
        {
            foreach (DataGridViewColumn column in grid.Columns)
            {
                var fieldKey = string.IsNullOrWhiteSpace(column.DataPropertyName)
                    ? column.Name
                    : column.DataPropertyName;

                HiveWinFormsFieldMetadata? fieldOverride = null;
                if (metadata is not null)
                    metadata.TryGetFieldOverride(fieldKey, out fieldOverride);

                fields.Add(
                    CreateDataSurfaceField(
                        column,
                        fieldKey,
                        fieldOverride));
            }
        }
        else
        {
            var dataSource = grid.DataSource;
            var bindingContext = grid.BindingContext;

            if (dataSource is not null && bindingContext is not null)
            {
                try
                {
                    var manager = bindingContext[
                        dataSource,
                        grid.DataMember];

                    var properties = manager?.GetItemProperties();

                    if (properties is not null)
                    {
                        foreach (PropertyDescriptor property in properties)
                        {
                            HiveWinFormsFieldMetadata? fieldOverride = null;
                            if (metadata is not null)
                            {
                                metadata.TryGetFieldOverride(
                                    property.Name,
                                    out fieldOverride);
                            }

                            fields.Add(
                                CreateDataSurfaceField(
                                    property.Name,
                                    property.PropertyType,
                                    property.IsReadOnly,
                                    fieldOverride));
                        }
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        if (metadata is not null)
        {
            foreach (var overrideEntry in metadata.FieldOverrides)
            {
                var consumed = fields.Any(field =>
                    string.Equals(
                        field.Name,
                        overrideEntry.Key,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        field.BindingMember,
                        overrideEntry.Key,
                        StringComparison.OrdinalIgnoreCase));

                if (!consumed)
                {
                    var columnMatch = grid.Columns
                        .Cast<DataGridViewColumn>()
                        .Any(column =>
                            string.Equals(
                                column.Name,
                                overrideEntry.Key,
                                StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(
                                column.DataPropertyName,
                                overrideEntry.Key,
                                StringComparison.OrdinalIgnoreCase));

                    if (!columnMatch)
                    {
                        throw new HiveWinFormsIntegrationException(
                            "hive.host.winforms.field-override-not-found",
                            $"The configured field override '{overrideEntry.Key}' is not present on data surface.");
                    }
                }
            }
        }

        return fields;
    }

    private static HiveHostFieldDescriptor CreateDataSurfaceField(
        DataGridViewColumn column,
        string fieldKey,
        HiveWinFormsFieldMetadata? metadata)
    {
        var name = CleanRequired(
            metadata?.Name,
            string.IsNullOrWhiteSpace(fieldKey)
                ? column.Name
                : fieldKey);

        var bindingMember =
            CleanOptional(metadata?.BindingMember) ??
            CleanOptional(column.DataPropertyName);

        return CreateDataSurfaceField(
            name,
            bindingMember,
            column.ValueType ?? typeof(string),
            column.ReadOnly,
            metadata);
    }

    private static HiveHostFieldDescriptor CreateDataSurfaceField(
        string propertyName,
        Type propertyType,
        bool readOnly,
        HiveWinFormsFieldMetadata? metadata)
    {
        return CreateDataSurfaceField(
            CleanRequired(metadata?.Name, propertyName),
            CleanOptional(metadata?.BindingMember) ?? propertyName,
            propertyType,
            readOnly,
            metadata);
    }

    private static HiveHostFieldDescriptor CreateDataSurfaceField(
        string name,
        string? bindingMember,
        Type valueType,
        bool readOnly,
        HiveWinFormsFieldMetadata? metadata)
    {
        var effectiveReadOnly =
            readOnly ||
            metadata?.ReadOnly == true ||
            metadata?.IsPrimaryKey == true;

        return new HiveHostFieldDescriptor(
            name,
            bindingMember,
            CleanRequired(
                metadata?.ValueType,
                valueType.FullName ?? typeof(string).FullName!),
            metadata?.Required ?? false,
            effectiveReadOnly,
            metadata?.Computed ?? false,
            metadata?.Generated ?? false,
            metadata?.IsPrimaryKey ?? false,
            currentValue: null,
            lookup: metadata?.Lookup);
    }

    private static List<HiveHostDataSurfaceDescriptor> ApplyParentChildRelationships(
        IReadOnlyList<(
            DataGridView Grid,
            HiveWinFormsDataSurfaceMetadata? Metadata,
            HiveHostDataSurfaceDescriptor Surface)> entries)
    {
        var duplicateSurface = entries
            .GroupBy(
                static entry => entry.Surface.Id,
                StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);

        if (duplicateSurface is not null)
        {
            throw new HiveWinFormsIntegrationException(
                "hive.host.winforms.surface-identity-duplicate",
                $"Data-surface identity '{duplicateSurface.Key}' is not unique within the captured host.");
        }

        var byId = entries.ToDictionary(
            static entry => entry.Surface.Id,
            static entry => entry.Surface,
            StringComparer.Ordinal);

        var childrenByParent =
            new Dictionary<string, List<HiveHostChildDataSurfaceDescriptor>>(
                StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            var metadata = entry.Metadata;
            if (metadata is null || !metadata.HasParentRelationship)
                continue;

            var parentSurfaceId = CleanOptional(metadata.ParentSurfaceId);
            var parentKeyField = CleanOptional(metadata.ParentKeyField);
            var childKeyField = CleanOptional(metadata.ChildKeyField);

            if (parentSurfaceId is null ||
                parentKeyField is null ||
                childKeyField is null)
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.child-surface-invalid",
                    $"The child data-surface relationship for '{entry.Surface.Id}' must define parent surface, parent key field, and child key field.");
            }

            if (string.Equals(
                    parentSurfaceId,
                    entry.Surface.Id,
                    StringComparison.Ordinal))
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.child-surface-self-reference",
                    $"Data surface '{entry.Surface.Id}' cannot be its own parent.");
            }

            if (!byId.TryGetValue(parentSurfaceId, out var parent))
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.child-parent-not-found",
                    $"Parent data surface '{parentSurfaceId}' was not found for child '{entry.Surface.Id}'.");
            }

            if (!parent.Fields.Any(field =>
                    string.Equals(
                        field.Name,
                        parentKeyField,
                        StringComparison.Ordinal)))
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.child-parent-key-not-found",
                    $"Parent key field '{parentKeyField}' was not found on data surface '{parentSurfaceId}'.");
            }

            if (!entry.Surface.Fields.Any(field =>
                    string.Equals(
                        field.Name,
                        childKeyField,
                        StringComparison.Ordinal)))
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.child-key-not-found",
                    $"Child key field '{childKeyField}' was not found on data surface '{entry.Surface.Id}'.");
            }

            var childDescriptor = new HiveHostChildDataSurfaceDescriptor(
                parentSurfaceId,
                entry.Surface.Id,
                parentKeyField,
                childKeyField);

            if (!childrenByParent.TryGetValue(
                    parentSurfaceId,
                    out var children))
            {
                children = new List<HiveHostChildDataSurfaceDescriptor>();
                childrenByParent.Add(parentSurfaceId, children);
            }

            if (children.Any(existing =>
                    string.Equals(
                        existing.ChildSurfaceId,
                        childDescriptor.ChildSurfaceId,
                        StringComparison.Ordinal)))
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.child-surface-duplicate",
                    $"Child data surface '{entry.Surface.Id}' is already related to parent '{parentSurfaceId}'.");
            }

            children.Add(childDescriptor);
        }

        var result = new List<HiveHostDataSurfaceDescriptor>(entries.Count);

        foreach (var entry in entries)
        {
            if (!childrenByParent.TryGetValue(
                    entry.Surface.Id,
                    out var children))
            {
                result.Add(entry.Surface);
                continue;
            }

            result.Add(
                new HiveHostDataSurfaceDescriptor(
                    entry.Surface.Id,
                    entry.Surface.Name,
                    entry.Surface.RowCount,
                    entry.Surface.Fields,
                    entry.Surface.Capabilities,
                    entry.Surface.Children.Concat(children)));
        }

        return result;
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

        var explicitId = CleanOptional(metadata?.SurfaceId);
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
            var overrideName = CleanOptional(
                hiveForm.HiveHostIntegration.HostName);

            if (overrideName is not null)
                return overrideName;
        }

        return rootName ?? rootRuntimeType;
    }

    private static string? GetExplicitControlId(Control control) =>
        control is IHiveWinFormsControl hiveControl
            ? CleanOptional(hiveControl.HiveIntegration.ControlId)
            : null;

    private Control? FindControlById(
        string id,
        CancellationToken cancellationToken)
    {
        const string prefix = "control:";

        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var key = id[prefix.Length..];

        var stack = new Stack<(Control Control, int Depth, string Path)>();
        stack.Push((_registration.Root, 0, "0"));

        var explicitMatch = new List<Control>();
        var namedMatch = new List<Control>();
        var visited = new HashSet<Control>(
            ReferenceEqualityComparer.Instance);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (control, depth, path) = stack.Pop();

            if (depth > _options.MaxDepth ||
                visited.Count >= _options.MaxNodes)
            {
                continue;
            }

            if (!visited.Add(control))
                continue;

            if (GetExplicitControlId(control) is { } explicitId &&
                string.Equals(
                    explicitId,
                    key,
                    StringComparison.Ordinal))
            {
                explicitMatch.Add(control);
            }

            if (string.Equals(
                    control.Name,
                    key,
                    StringComparison.Ordinal))
            {
                namedMatch.Add(control);
            }

            if (control.IsDisposed || control.Disposing)
                continue;

            if (depth >= _options.MaxDepth)
                continue;

            for (var index = control.Controls.Count - 1; index >= 0; index--)
            {
                stack.Push((
                    control.Controls[index],
                    depth + 1,
                    path + "/" + index));
            }
        }

        if (explicitMatch.Count == 1)
            return explicitMatch[0];

        if (explicitMatch.Count > 1 ||
            namedMatch.Count > 1)
        {
            return null;
        }

        if (namedMatch.Count == 1)
            return namedMatch[0];

        return key.StartsWith("0", StringComparison.Ordinal) &&
               key.Contains('/', StringComparison.Ordinal)
            ? FindControl(key)
            : null;
    }

    private Control? FindControl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        var parts = path.Split('/');
        if (parts.Length == 0 || parts[0] != "0")
            return null;

        Control current = _registration.Root;

        for (var index = 1; index < parts.Length; index++)
        {
            if (!int.TryParse(
                    parts[index],
                    System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var childIndex) ||
                childIndex < 0 ||
                childIndex >= current.Controls.Count)
            {
                return null;
            }

            current = current.Controls[childIndex];
        }

        return current;
    }

    private static bool SupportsStandardField(Control control) =>
        control is
            (TextBoxBase or CheckBox or ComboBox or DateTimePicker or NumericUpDown);

    private static bool CanSetStandardValue(
        Control control,
        HiveHostFieldDescriptor? field = null)
    {
        if (!control.Enabled ||
            IsStandardReadOnly(control))
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

        return control is
            (TextBoxBase or CheckBox or DateTimePicker or NumericUpDown) ||
            control is ComboBox comboBox &&
            comboBox.DropDownStyle != ComboBoxStyle.DropDownList;
    }

    private static bool IsStandardReadOnly(Control control) =>
        control switch
        {
            TextBox textBox => textBox.ReadOnly,
            RichTextBox richTextBox => richTextBox.ReadOnly,
            MaskedTextBox maskedTextBox => maskedTextBox.ReadOnly,
            NumericUpDown numericUpDown => numericUpDown.ReadOnly,
            _ => false
        };

    private static HiveHostCapabilityDescriptor CreateCapability(
        string key,
        HiveHostCapabilityKind kind,
        string name,
        HiveHostActionKind? action = null) =>
        new(
            CreateCapabilityId(key),
            kind,
            name,
            action: action);

    private static Guid CreateCapabilityId(string key)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(
                "Hive.Host.WinForms.Capability.V1|" + key));

        return new Guid(bytes.AsSpan(0, 16));
    }

    private static int TryGetBoundRowCount(DataGridView grid)
    {
        var dataSource = grid.DataSource;

        if (dataSource is not null)
        {
            var bindingContext = grid.BindingContext;
            if (bindingContext is not null)
            {
                try
                {
                    var manager = bindingContext[
                        dataSource,
                        grid.DataMember];

                    if (manager is not null)
                        return Math.Max(0, manager.Count);
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        return grid.AllowUserToAddRows
            ? Math.Max(0, grid.Rows.Count - 1)
            : grid.Rows.Count;
    }

    private static string GetValueTypeName(Control control) =>
        control switch
        {
            TextBoxBase => typeof(string).FullName!,
            CheckBox => typeof(bool).FullName!,
            ComboBox => typeof(string).FullName!,
            DateTimePicker => typeof(DateTime).FullName!,
            NumericUpDown => typeof(decimal).FullName!,
            _ => typeof(string).FullName!
        };

    private static HiveHostValue? TryReadValue(Control control)
    {
        if (control is TextBox passwordTextBox &&
            (passwordTextBox.UseSystemPasswordChar ||
             passwordTextBox.PasswordChar != ' '))
        {
            return null;
        }

        if (control is MaskedTextBox maskedTextBox &&
            maskedTextBox.PasswordChar != ' ')
        {
            return null;
        }

        return control switch
        {
            TextBoxBase textControl =>
                HiveHostValue.FromString(textControl.Text),
            CheckBox checkBox =>
                HiveHostValue.FromBoolean(checkBox.Checked),
            ComboBox comboBox =>
                HiveHostValue.FromString(comboBox.Text),
            DateTimePicker dateTimePicker =>
                HiveHostValue.FromDateTime(dateTimePicker.Value),
            NumericUpDown numericUpDown =>
                HiveHostValue.FromDecimal(numericUpDown.Value),
            _ => null
        };
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

        if (IsStandardReadOnly(control))
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Conflict(
                    "hive.host.winforms.control-read-only",
                    "The requested WinForms control is read-only."));
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

        if (control is TextBoxBase textBox)
        {
            if (request.Value is not { } value ||
                value.Kind != HiveHostValueKind.String)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.value-type-invalid",
                        "A string value is required for a text control."));
            }

            if (control is TextBox password &&
                (password.UseSystemPasswordChar ||
                 password.PasswordChar != ' '))
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Unsupported(
                        "hive.host.winforms.password-write-unsupported",
                        "Password controls are not handled by the reusable value adapter."));
            }

            textBox.Text = value.AsString()!;
        }
        else if (control is CheckBox checkBox)
        {
            if (request.Value is not { } value ||
                !value.TryGetBoolean(out var boolean))
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.value-type-invalid",
                        "A boolean value is required for a check box."));
            }

            checkBox.Checked = boolean;
        }
        else if (control is DateTimePicker dateTimePicker)
        {
            if (request.Value is not { } value ||
                !value.TryGetDateTime(out var dateTime))
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.value-type-invalid",
                        "A date/time value is required for a date-time control."));
            }

            if (dateTime < dateTimePicker.MinDate ||
                dateTime > dateTimePicker.MaxDate)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.datetime-range-invalid",
                        "The requested date/time value is outside the host control range."));
            }

            dateTimePicker.Value = dateTime;
        }
        else if (control is NumericUpDown numericUpDown)
        {
            if (request.Value is not { } value ||
                !value.TryGetDecimal(out var decimalValue))
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.value-type-invalid",
                        "A decimal value is required for a numeric control."));
            }

            if (decimalValue < numericUpDown.Minimum ||
                decimalValue > numericUpDown.Maximum)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.numeric-range-invalid",
                        "The requested numeric value is outside the host control range."));
            }

            numericUpDown.Value = decimalValue;
        }
        else if (control is ComboBox comboBox)
        {
            if (comboBox.DropDownStyle == ComboBoxStyle.DropDownList)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Unsupported(
                        "hive.host.winforms.combo-selection-requires-lookup",
                        "Selection in a drop-down list must use the bounded lookup contract."));
            }

            if (request.Value is not { } value ||
                value.Kind != HiveHostValueKind.String)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    Error.Validation(
                        "hive.host.winforms.value-type-invalid",
                        "A string value is required for a combo box."));
            }

            comboBox.Text = value.AsString()!;
        }
        else
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.interaction-unsupported",
                    "The reusable adapter does not support setting this control type."));
        }

        return Result<HiveHostInteractionResult>.Success(
            new HiveHostInteractionResult(
                request.CorrelationId,
                request.Kind,
                TryReadValue(control)));
    }

    private static string CleanRequired(
        string? overrideValue,
        string fallback)
    {
        if (!string.IsNullOrWhiteSpace(overrideValue))
            return overrideValue.Trim();

        return fallback.Trim();
    }

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}

public sealed class HiveWinFormsIntegrationException : Exception
{
    public HiveWinFormsIntegrationException(
        string code,
        string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
