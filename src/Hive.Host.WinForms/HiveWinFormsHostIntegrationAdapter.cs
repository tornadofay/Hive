using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Hive.Core;

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
    private readonly Dictionary<string, Guid> _capabilityIds =
        new(StringComparer.Ordinal);
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
        _context = new HiveWinFormsHostContext(accessContext, options);
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

        var snapshot = await _context
            .CaptureAsync(_registration, cancellationToken)
            .ConfigureAwait(false);

        if (snapshot.IsFailure)
            return Result<HiveHostContextDescriptor>.Failure(snapshot.Error!);

        var controls = new List<HiveHostControlDescriptor>(
            snapshot.Value!.Controls.Count);

        var dataSurfaces = new List<HiveHostDataSurfaceDescriptor>();

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

            controls.Add(CreateControlDescriptor(controlSnapshot, control));

            if (control is DataGridView grid)
            {
                var surfaceId = "surface:" + controlSnapshot.Path;

                if (_semanticProvider is not null &&
                    _semanticProvider.TryDescribeDataSurface(
                        grid,
                        surfaceId,
                        out var semanticSurface))
                {
                    dataSurfaces.Add(semanticSurface);
                }
                else
                {
                    dataSurfaces.Add(
                        CreateDefaultDataSurface(grid, surfaceId));
                }
            }
        }

        return Result<HiveHostContextDescriptor>.Success(
            new HiveHostContextDescriptor(
                snapshot.Value.Provenance.RegistrationId,
                snapshot.Value.RootName ??
                snapshot.Value.RootRuntimeType,
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
            HiveHostInteractionKind.DeleteRow)
        {
            return await ExecuteWithProviderAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }

        if (request.Kind == HiveHostInteractionKind.InvokeAction)
        {
            return await ExecuteWithProviderAsync(
                request,
                cancellationToken).ConfigureAwait(false);
        }

        if (request.ControlId is null)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Validation(
                    "hive.host.winforms.control-required",
                    "A control identity is required for this interaction."));
        }

        var control = FindControlById(request.ControlId);
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

        var capabilitySuffix = request.Kind switch
        {
            HiveHostInteractionKind.ReadControl => ":read",
            HiveHostInteractionKind.SetControlValue => ":set",
            _ => null
        };

        if (capabilitySuffix is not null)
        {
            var expectedKey = request.ControlId + capabilitySuffix;
            if (!_capabilityIds.TryGetValue(expectedKey, out var expectedCapabilityId))
            {
                return Result<HiveHostInteractionResult>.Failure(
                    new Error(
                        "hive.host.winforms.capability-not-found",
                        ErrorCategory.NotFound,
                        "The requested WinForms capability is not part of the registered host context."));
            }

            if (expectedCapabilityId != request.CapabilityId)
            {
                return Result<HiveHostInteractionResult>.Failure(
                    new Error(
                        "hive.host.winforms.capability-mismatch",
                        ErrorCategory.Forbidden,
                        "The requested capability is not authorized for the supplied WinForms target."));
            }
        }

        if (request.ExpectedHostVersion is not null)
        {
            return Result<HiveHostInteractionResult>.Failure(
                Error.Unsupported(
                    "hive.host.winforms.host-version-unsupported",
                    "The reusable standard-control adapter does not provide host-version concurrency evidence."));
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
        Control control)
    {
        var field = CreateStandardField(control, snapshot);
        var capabilities = new List<HiveHostCapabilityDescriptor>();

        if (field is not null)
        {
            capabilities.Add(
                CreateCapability(
                    "control:" + snapshot.Path + ":read",
                    HiveHostCapabilityKind.ReadControl,
                    "Read control value"));
        }

        if (CanSetStandardValue(control, snapshot))
        {
            capabilities.Add(
                CreateCapability(
                    "control:" + snapshot.Path + ":set",
                    HiveHostCapabilityKind.SetControlValue,
                    "Set control value"));
        }

        return new HiveHostControlDescriptor(
            "control:" + snapshot.Path,
            snapshot.Path,
            snapshot.Depth,
            snapshot.RuntimeType,
            snapshot.Name,
            snapshot.AccessibleName,
            snapshot.Visible,
            snapshot.Enabled,
            snapshot.Focused,
            snapshot.ReadOnly,
            field,
            capabilities);
    }

    private HiveHostFieldDescriptor? CreateStandardField(
        Control control,
        HiveWinFormsControlSnapshot snapshot)
    {
        if (control is not
            (TextBoxBase or CheckBox or ComboBox or DateTimePicker or NumericUpDown))
        {
            return null;
        }

        var bindingMember = snapshot.Bindings
            .Select(static binding => binding.BindingMember)
            .FirstOrDefault(static member => !string.IsNullOrWhiteSpace(member));

        var value = TryReadValue(control);

        return new HiveHostFieldDescriptor(
            bindingMember ?? snapshot.Name ?? snapshot.Path,
            bindingMember,
            GetValueTypeName(control),
            required: false,
            readOnly: snapshot.ReadOnly,
            computed: false,
            generated: false,
            isPrimaryKey: false,
            currentValue: value);
    }

    private HiveHostDataSurfaceDescriptor CreateDefaultDataSurface(
        DataGridView grid,
        string surfaceId)
    {
        var fields = grid.Columns
            .Cast<DataGridViewColumn>()
            .Select(column =>
                new HiveHostFieldDescriptor(
                    string.IsNullOrWhiteSpace(column.DataPropertyName)
                        ? column.Name
                        : column.DataPropertyName,
                    column.DataPropertyName,
                    column.ValueType?.FullName ?? typeof(string).FullName!,
                    required: false,
                    readOnly: column.ReadOnly,
                    computed: false,
                    generated: false,
                    isPrimaryKey: false))
            .ToArray();

        var rowCount = TryGetBoundRowCount(grid);

        var capabilities = new[]
        {
            CreateCapability(
                surfaceId + ":read",
                HiveHostCapabilityKind.ReadDataSurface,
                "Read data surface")
        };

        return new HiveHostDataSurfaceDescriptor(
            surfaceId,
            grid.Name.Length == 0 ? "WinForms data surface" : grid.Name,
            rowCount,
            fields,
            capabilities);
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
                    // Fall back to the materialized grid rows when the bound
                    // source cannot provide a currency manager.
                }
                catch (InvalidOperationException)
                {
                    // Fall back to the materialized grid rows when the bound
                    // source is not currently available.
                }
            }
        }
        return grid.AllowUserToAddRows
            ? Math.Max(0, grid.Rows.Count - 1)
            : grid.Rows.Count;
    }

    private HiveHostCapabilityDescriptor CreateCapability(
        string key,
        HiveHostCapabilityKind kind,
        string name)
    {
        if (!_capabilityIds.TryGetValue(key, out var id))
        {
            id = Guid.NewGuid();
            _capabilityIds.Add(key, id);
        }

        return new HiveHostCapabilityDescriptor(id, kind, name);
    }

    private Control? FindControlById(string id)
    {
        const string prefix = "control:";

        if (!id.StartsWith(prefix, StringComparison.Ordinal))
            return null;

        var path = id[prefix.Length..];
        return FindControl(path);
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
            (passwordTextBox.UseSystemPasswordChar || passwordTextBox.PasswordChar != '\0'))
        {
            return null;
        }

        if (control is MaskedTextBox maskedTextBox &&
            maskedTextBox.PasswordChar != '\0')
        {
            return null;
        }

        return control switch
        {
            TextBoxBase textControl => HiveHostValue.FromString(textControl.Text),
            CheckBox checkBox => HiveHostValue.FromBoolean(checkBox.Checked),
            ComboBox comboBox => HiveHostValue.FromString(comboBox.Text),
            DateTimePicker dateTimePicker =>
                HiveHostValue.FromDateTime(dateTimePicker.Value),
            NumericUpDown numericUpDown =>
                HiveHostValue.FromDecimal(numericUpDown.Value),
            _ => (HiveHostValue?)null
        };
    }

    private static bool CanSetStandardValue(
        Control control,
        HiveWinFormsControlSnapshot snapshot) =>
        snapshot.Enabled &&
        !IsStandardReadOnly(control) &&
        control is
            (TextBoxBase or CheckBox or DateTimePicker or NumericUpDown) ||
        control is ComboBox comboBox &&
        snapshot.Enabled &&
        !IsStandardReadOnly(control) &&
        comboBox.DropDownStyle != ComboBoxStyle.DropDownList;

    private static bool IsStandardReadOnly(Control control) =>
        control switch
        {
            TextBox textBox => textBox.ReadOnly,
            RichTextBox richTextBox => richTextBox.ReadOnly,
            MaskedTextBox maskedTextBox => maskedTextBox.ReadOnly,
            NumericUpDown numericUpDown => numericUpDown.ReadOnly,
            _ => false
        };

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
                (password.UseSystemPasswordChar || password.PasswordChar != '\0'))
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

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref _disposed) != 0,
            this);
    }
}
