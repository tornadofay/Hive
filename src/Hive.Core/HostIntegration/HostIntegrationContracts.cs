using System.Collections.ObjectModel;

namespace Hive.Core;

public enum HiveHostCapabilityKind
{
    ReadControl,
    SetControlValue,
    ReadDataSurface,
    ReadRow,
    AddRow,
    EditRow,
    DeleteRow,
    ResolveLookup,
    InvokeAction,
    BusinessOperation
}

public enum HiveHostInteractionKind
{
    ReadControl,
    SetControlValue,
    ReadRow,
    AddRow,
    EditRow,
    DeleteRow,
    InvokeAction
}

public enum HiveHostActionKind
{
    New,
    Edit,
    Save,
    Delete,
    Reload,
    Move,
    Search,
    Report,
    Print,
    Preview
}

public enum HiveBusinessOperationImplementation
{
    Api,
    Ui,
    ApiAndUi
}

public enum HiveHostValueKind
{
    Null,
    String,
    Boolean,
    Int64,
    Decimal,
    DateTime,
    Guid
}

public readonly record struct HiveHostValue
{
    private readonly string? _value;

    private HiveHostValue(HiveHostValueKind kind, string? value)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (kind == HiveHostValueKind.Null && value is not null)
            throw new ArgumentException("Null values cannot contain text.", nameof(value));

        if (kind != HiveHostValueKind.Null && value is null)
            throw new ArgumentNullException(nameof(value));

        Kind = kind;
        _value = value;
    }

    public HiveHostValueKind Kind { get; }

    public static HiveHostValue Null => new(HiveHostValueKind.Null, null);

    public static HiveHostValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new(HiveHostValueKind.String, value);
    }

    public static HiveHostValue FromBoolean(bool value) =>
        new(HiveHostValueKind.Boolean, value ? "true" : "false");

    public static HiveHostValue FromInt64(long value) =>
        new(HiveHostValueKind.Int64, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static HiveHostValue FromDecimal(decimal value) =>
        new(HiveHostValueKind.Decimal, value.ToString(System.Globalization.CultureInfo.InvariantCulture));

    public static HiveHostValue FromDateTime(DateTime value) =>
        new(
            HiveHostValueKind.DateTime,
            value.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture));

    public static HiveHostValue FromGuid(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A GUID value cannot be empty.", nameof(value));

        return new(HiveHostValueKind.Guid, value.ToString("D"));
    }

    public string? AsString() => _value;

    public bool TryGetBoolean(out bool value) =>
        Kind == HiveHostValueKind.Boolean &&
        bool.TryParse(_value, out value);

    public bool TryGetInt64(out long value) =>
        Kind == HiveHostValueKind.Int64 &&
        long.TryParse(
            _value,
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);

    public bool TryGetDecimal(out decimal value) =>
        Kind == HiveHostValueKind.Decimal &&
        decimal.TryParse(
            _value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);

    public bool TryGetDateTime(out DateTime value)
    {
        if (Kind != HiveHostValueKind.DateTime ||
            !DateTime.TryParse(
                _value,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            value = default;
            return false;
        }

        value = parsed;
        return true;
    }

    public bool TryGetGuid(out Guid value) =>
        Kind == HiveHostValueKind.Guid &&
        Guid.TryParse(_value, out value) &&
        value != Guid.Empty;

    public override string ToString() => _value ?? string.Empty;
}

public readonly record struct HiveHostRowIdentity
{
    public HiveHostRowIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record HiveHostProvenance
{
    public HiveHostProvenance(
        Guid registrationId,
        Guid captureId,
        DateTimeOffset capturedAtUtc,
        CorrelationId correlationId,
        string adapterId,
        ResourceAccessContext accessContext)
    {
        if (registrationId == Guid.Empty)
            throw new ArgumentException("Registration identity is required.", nameof(registrationId));

        if (captureId == Guid.Empty)
            throw new ArgumentException("Capture identity is required.", nameof(captureId));

        if (correlationId == default)
            throw new ArgumentException("Correlation identity is required.", nameof(correlationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);
        ArgumentNullException.ThrowIfNull(accessContext);

        RegistrationId = registrationId;
        CaptureId = captureId;
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
        CorrelationId = correlationId;
        AdapterId = adapterId.Trim();
        AccessContext = accessContext;
    }

    public Guid RegistrationId { get; }

    public Guid CaptureId { get; }

    public DateTimeOffset CapturedAtUtc { get; }

    public CorrelationId CorrelationId { get; }

    public string AdapterId { get; }

    public ResourceAccessContext AccessContext { get; }
}

public sealed record HiveHostCapabilityDescriptor
{
    public HiveHostCapabilityDescriptor(
        Guid id,
        HiveHostCapabilityKind kind,
        string name,
        bool supported = true,
        HiveHostActionKind? action = null)
    {
        if (id == Guid.Empty)
            throw new ArgumentException("Capability identity is required.", nameof(id));

        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (action is not null &&
            kind != HiveHostCapabilityKind.InvokeAction)
        {
            throw new ArgumentException(
                "An action can only be supplied for InvokeAction capabilities.",
                nameof(action));
        }

        Id = id;
        Kind = kind;
        Name = name.Trim();
        Supported = supported;
        Action = action;
    }

    public Guid Id { get; }

    public HiveHostCapabilityKind Kind { get; }

    public string Name { get; }

    public bool Supported { get; }

    public HiveHostActionKind? Action { get; }
}

public sealed record HiveHostFieldDescriptor
{
    public HiveHostFieldDescriptor(
        string name,
        string? bindingMember,
        string valueType,
        bool required,
        bool readOnly,
        bool computed,
        bool generated,
        bool isPrimaryKey,
        HiveHostValue? currentValue = null,
        HiveHostLookupDescriptor? lookup = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueType);

        if (computed && isPrimaryKey)
            throw new ArgumentException("A computed field cannot also be a primary-key field.", nameof(isPrimaryKey));

        Name = name.Trim();
        BindingMember = string.IsNullOrWhiteSpace(bindingMember) ? null : bindingMember.Trim();
        ValueType = valueType.Trim();
        Required = required;
        ReadOnly = readOnly;
        Computed = computed;
        Generated = generated;
        IsPrimaryKey = isPrimaryKey;
        CurrentValue = currentValue;
        Lookup = lookup;
    }

    public string Name { get; }

    public string? BindingMember { get; }

    public string ValueType { get; }

    public bool Required { get; }

    public bool ReadOnly { get; }

    public bool Computed { get; }

    public bool Generated { get; }

    public bool IsPrimaryKey { get; }

    public HiveHostValue? CurrentValue { get; }

    public HiveHostLookupDescriptor? Lookup { get; }
}

public sealed record HiveHostControlDescriptor
{
    public HiveHostControlDescriptor(
        string id,
        string path,
        int depth,
        string runtimeType,
        string? name,
        string? label,
        bool visible,
        bool enabled,
        bool focused,
        bool readOnly,
        HiveHostFieldDescriptor? field,
        IEnumerable<HiveHostCapabilityDescriptor> capabilities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeType);

        if (depth < 0)
            throw new ArgumentOutOfRangeException(nameof(depth));

        Id = id.Trim();
        Path = path.Trim();
        Depth = depth;
        RuntimeType = runtimeType.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim();
        Visible = visible;
        Enabled = enabled;
        Focused = focused;
        ReadOnly = readOnly;
        Field = field;
        Capabilities = CopyCapabilities(capabilities);
    }

    public string Id { get; }

    public string Path { get; }

    public int Depth { get; }

    public string RuntimeType { get; }

    public string? Name { get; }

    public string? Label { get; }

    public bool Visible { get; }

    public bool Enabled { get; }

    public bool Focused { get; }

    public bool ReadOnly { get; }

    public HiveHostFieldDescriptor? Field { get; }

    public IReadOnlyList<HiveHostCapabilityDescriptor> Capabilities { get; }

    private static IReadOnlyList<HiveHostCapabilityDescriptor> CopyCapabilities(
        IEnumerable<HiveHostCapabilityDescriptor> capabilities)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        var items = capabilities.ToArray();

        if (items.Any(static item => item is null))
            throw new ArgumentException("Capabilities cannot contain null values.", nameof(capabilities));

        return new ReadOnlyCollection<HiveHostCapabilityDescriptor>(items);
    }
}

public sealed record HiveHostRow
{
    public HiveHostRow(
        HiveHostRowIdentity identity,
        int position,
        IReadOnlyDictionary<string, HiveHostValue> values)
    {
        if (identity == default)
            throw new ArgumentException("Stable row identity is required.", nameof(identity));

        if (position < 0)
            throw new ArgumentOutOfRangeException(nameof(position));

        ArgumentNullException.ThrowIfNull(values);

        var copy = new Dictionary<string, HiveHostValue>(values, StringComparer.Ordinal);
        if (copy.Keys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Row field names cannot be blank.", nameof(values));

        Identity = identity;
        Position = position;
        Values = new ReadOnlyDictionary<string, HiveHostValue>(copy);
    }

    public HiveHostRowIdentity Identity { get; }

    public int Position { get; }

    public IReadOnlyDictionary<string, HiveHostValue> Values { get; }
}

public sealed record HiveHostChildDataSurfaceDescriptor
{
    public HiveHostChildDataSurfaceDescriptor(
        string surfaceId,
        string childSurfaceId,
        string parentKeyField,
        string childKeyField)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(surfaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(childSurfaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentKeyField);
        ArgumentException.ThrowIfNullOrWhiteSpace(childKeyField);

        SurfaceId = surfaceId.Trim();
        ChildSurfaceId = childSurfaceId.Trim();
        ParentKeyField = parentKeyField.Trim();
        ChildKeyField = childKeyField.Trim();
    }

    public string SurfaceId { get; }

    public string ChildSurfaceId { get; }

    public string ParentKeyField { get; }

    public string ChildKeyField { get; }
}

public sealed record HiveHostDataSurfaceDescriptor
{
    public HiveHostDataSurfaceDescriptor(
        string id,
        string name,
        int rowCount,
        IEnumerable<HiveHostFieldDescriptor> fields,
        IEnumerable<HiveHostCapabilityDescriptor> capabilities,
        IEnumerable<HiveHostChildDataSurfaceDescriptor>? children = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (rowCount < 0)
            throw new ArgumentOutOfRangeException(nameof(rowCount));

        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(capabilities);

        Id = id.Trim();
        Name = name.Trim();
        RowCount = rowCount;
        Fields = new ReadOnlyCollection<HiveHostFieldDescriptor>(fields.ToArray());
        Capabilities = new ReadOnlyCollection<HiveHostCapabilityDescriptor>(capabilities.ToArray());
        Children = new ReadOnlyCollection<HiveHostChildDataSurfaceDescriptor>(
            (children ?? Array.Empty<HiveHostChildDataSurfaceDescriptor>()).ToArray());
    }

    public string Id { get; }

    public string Name { get; }

    public int RowCount { get; }

    public IReadOnlyList<HiveHostFieldDescriptor> Fields { get; }

    public IReadOnlyList<HiveHostCapabilityDescriptor> Capabilities { get; }

    public IReadOnlyList<HiveHostChildDataSurfaceDescriptor> Children { get; }
}

public sealed record HiveHostLookupDescriptor
{
    public HiveHostLookupDescriptor(
        string id,
        Guid capabilityId,
        string displayField,
        string valueField,
        IEnumerable<string>? dependentFields = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        if (capabilityId == Guid.Empty)
            throw new ArgumentException("Capability identity is required.", nameof(capabilityId));

        ArgumentException.ThrowIfNullOrWhiteSpace(displayField);
        ArgumentException.ThrowIfNullOrWhiteSpace(valueField);

        Id = id.Trim();
        CapabilityId = capabilityId;
        DisplayField = displayField.Trim();
        ValueField = valueField.Trim();

        var dependencies = (dependentFields ?? Array.Empty<string>())
            .Select(static field => field?.Trim())
            .Where(static field => !string.IsNullOrWhiteSpace(field))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        DependentFields = new ReadOnlyCollection<string>(dependencies);
    }

    public string Id { get; }

    public Guid CapabilityId { get; }

    public string DisplayField { get; }

    public string ValueField { get; }

    public IReadOnlyList<string> DependentFields { get; }
}

public sealed record HiveLookupOption
{
    public HiveLookupOption(
        HiveHostRowIdentity identity,
        HiveHostValue value,
        string display)
    {
        if (identity == default)
            throw new ArgumentException("Lookup option identity is required.", nameof(identity));

        ArgumentException.ThrowIfNullOrWhiteSpace(display);

        Identity = identity;
        Value = value;
        Display = display.Trim();
    }

    public HiveHostRowIdentity Identity { get; }

    public HiveHostValue Value { get; }

    public string Display { get; }
}

public sealed record HiveLookupRequest
{
    public HiveLookupRequest(
        string lookupId,
        Guid capabilityId,
        IReadOnlyDictionary<string, HiveHostValue>? currentValues = null,
        IReadOnlyDictionary<string, HiveHostValue>? hostContext = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lookupId);

        if (capabilityId == Guid.Empty)
            throw new ArgumentException("Capability identity is required.", nameof(capabilityId));

        LookupId = lookupId.Trim();
        CapabilityId = capabilityId;
        CurrentValues = CopyValues(currentValues);
        HostContext = CopyValues(hostContext);
    }

    public string LookupId { get; }

    public Guid CapabilityId { get; }

    public IReadOnlyDictionary<string, HiveHostValue> CurrentValues { get; }

    public IReadOnlyDictionary<string, HiveHostValue> HostContext { get; }

    private static IReadOnlyDictionary<string, HiveHostValue> CopyValues(
        IReadOnlyDictionary<string, HiveHostValue>? values)
    {
        var copy = new Dictionary<string, HiveHostValue>(
            values ?? new Dictionary<string, HiveHostValue>(),
            StringComparer.Ordinal);

        if (copy.Keys.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Context keys cannot be blank.", nameof(values));

        return new ReadOnlyDictionary<string, HiveHostValue>(copy);
    }
}

public sealed record HiveHostBusinessOperationDescriptor
{
    public HiveHostBusinessOperationDescriptor(
        Guid capabilityId,
        string operationType,
        string displayName,
        HiveBusinessOperationImplementation implementation)
    {
        if (capabilityId == Guid.Empty)
            throw new ArgumentException("Capability identity is required.", nameof(capabilityId));

        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        if (!Enum.IsDefined(implementation))
            throw new ArgumentOutOfRangeException(nameof(implementation));

        CapabilityId = capabilityId;
        OperationType = operationType.Trim();
        DisplayName = displayName.Trim();
        Implementation = implementation;
    }

    public Guid CapabilityId { get; }

    public string OperationType { get; }

    public string DisplayName { get; }

    public HiveBusinessOperationImplementation Implementation { get; }

    public IReadOnlyList<HiveBusinessOperationImplementation> Stages =>
        Implementation switch
        {
            HiveBusinessOperationImplementation.Api =>
                new[] { HiveBusinessOperationImplementation.Api },
            HiveBusinessOperationImplementation.Ui =>
                new[] { HiveBusinessOperationImplementation.Ui },
            HiveBusinessOperationImplementation.ApiAndUi =>
                new[]
                {
                    HiveBusinessOperationImplementation.Api,
                    HiveBusinessOperationImplementation.Ui
                },
            _ => throw new InvalidOperationException("Business-operation implementation is invalid.")
        };
}

public sealed record HiveHostContextDescriptor
{
    public HiveHostContextDescriptor(
        Guid registrationId,
        string hostName,
        HiveHostProvenance provenance,
        IEnumerable<HiveHostControlDescriptor> controls,
        IEnumerable<HiveHostDataSurfaceDescriptor> dataSurfaces,
        IEnumerable<HiveHostBusinessOperationDescriptor> businessOperations)
    {
        if (registrationId == Guid.Empty)
            throw new ArgumentException("Registration identity is required.", nameof(registrationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentNullException.ThrowIfNull(provenance);
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(dataSurfaces);
        ArgumentNullException.ThrowIfNull(businessOperations);

        RegistrationId = registrationId;
        HostName = hostName.Trim();
        Provenance = provenance;
        Controls = new ReadOnlyCollection<HiveHostControlDescriptor>(controls.ToArray());
        DataSurfaces = new ReadOnlyCollection<HiveHostDataSurfaceDescriptor>(dataSurfaces.ToArray());
        BusinessOperations = new ReadOnlyCollection<HiveHostBusinessOperationDescriptor>(businessOperations.ToArray());
    }

    public Guid RegistrationId { get; }

    public string HostName { get; }

    public HiveHostProvenance Provenance { get; }

    public IReadOnlyList<HiveHostControlDescriptor> Controls { get; }

    public IReadOnlyList<HiveHostDataSurfaceDescriptor> DataSurfaces { get; }

    public IReadOnlyList<HiveHostBusinessOperationDescriptor> BusinessOperations { get; }
}

public sealed record HiveHostCapabilityRequest
{
    public HiveHostCapabilityRequest(
        Guid capabilityId,
        HiveHostCapabilityKind capabilityKind,
        CorrelationId correlationId,
        string adapterId,
        ResourceReference? source = null,
        string? controlId = null,
        string? surfaceId = null,
        HiveHostRowIdentity? rowIdentity = null,
        string? fieldName = null,
        HiveHostActionKind? action = null)
    {
        if (capabilityId == Guid.Empty)
            throw new ArgumentException("Capability identity is required.", nameof(capabilityId));

        if (!Enum.IsDefined(capabilityKind))
            throw new ArgumentOutOfRangeException(nameof(capabilityKind));

        if (correlationId == default)
            throw new ArgumentException("Correlation identity is required.", nameof(correlationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);

        if (action is not null &&
            capabilityKind != HiveHostCapabilityKind.InvokeAction)
        {
            throw new ArgumentException(
                "An action can only be supplied for InvokeAction capabilities.",
                nameof(action));
        }

        CapabilityId = capabilityId;
        CapabilityKind = capabilityKind;
        CorrelationId = correlationId;
        AdapterId = adapterId.Trim();
        Source = source;
        ControlId = Clean(controlId);
        SurfaceId = Clean(surfaceId);
        RowIdentity = rowIdentity;
        FieldName = Clean(fieldName);
        Action = action;
    }

    public Guid CapabilityId { get; }

    public HiveHostCapabilityKind CapabilityKind { get; }

    public CorrelationId CorrelationId { get; }

    public string AdapterId { get; }

    public ResourceReference? Source { get; }

    public string? ControlId { get; }

    public string? SurfaceId { get; }

    public HiveHostRowIdentity? RowIdentity { get; }

    public string? FieldName { get; }

    public HiveHostActionKind? Action { get; }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record HiveHostInteractionRequest
{
    public HiveHostInteractionRequest(
        Guid capabilityId,
        HiveHostInteractionKind kind,
        CorrelationId correlationId,
        string? controlId = null,
        string? surfaceId = null,
        HiveHostRowIdentity? rowIdentity = null,
        string? fieldName = null,
        HiveHostValue? value = null,
        HiveHostActionKind? action = null,
        string? expectedHostVersion = null)
    {
        if (capabilityId == Guid.Empty)
            throw new ArgumentException("Capability identity is required.", nameof(capabilityId));

        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        if (correlationId == default)
            throw new ArgumentException("Correlation identity is required.", nameof(correlationId));

        if (kind is HiveHostInteractionKind.SetControlValue &&
            value is null)
        {
            throw new ArgumentException("A value is required for SetControlValue.", nameof(value));
        }

        if (kind is HiveHostInteractionKind.InvokeAction &&
            action is null)
        {
            throw new ArgumentException("An action is required for InvokeAction.", nameof(action));
        }

        CapabilityId = capabilityId;
        Kind = kind;
        CorrelationId = correlationId;
        ControlId = Clean(controlId);
        SurfaceId = Clean(surfaceId);
        RowIdentity = rowIdentity;
        FieldName = Clean(fieldName);
        Value = value;
        Action = action;
        ExpectedHostVersion = Clean(expectedHostVersion);
    }

    public Guid CapabilityId { get; }

    public HiveHostInteractionKind Kind { get; }

    public CorrelationId CorrelationId { get; }

    public string? ControlId { get; }

    public string? SurfaceId { get; }

    public HiveHostRowIdentity? RowIdentity { get; }

    public string? FieldName { get; }

    public HiveHostValue? Value { get; }

    public HiveHostActionKind? Action { get; }

    public string? ExpectedHostVersion { get; }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record HiveHostInteractionResult
{
    public HiveHostInteractionResult(
        CorrelationId correlationId,
        HiveHostInteractionKind kind,
        HiveHostValue? resultValue = null,
        HiveHostRow? row = null,
        HiveHostRowIdentity? resultingRowIdentity = null,
        string? hostVersion = null)
    {
        if (correlationId == default)
            throw new ArgumentException("Correlation identity is required.", nameof(correlationId));

        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind));

        CorrelationId = correlationId;
        Kind = kind;
        ResultValue = resultValue;
        Row = row;
        ResultingRowIdentity = resultingRowIdentity;
        HostVersion = string.IsNullOrWhiteSpace(hostVersion) ? null : hostVersion.Trim();
    }

    public CorrelationId CorrelationId { get; }

    public HiveHostInteractionKind Kind { get; }

    public HiveHostValue? ResultValue { get; }

    public HiveHostRow? Row { get; }

    public HiveHostRowIdentity? ResultingRowIdentity { get; }

    public string? HostVersion { get; }
}

public sealed record HiveBusinessOperationComposition
{
    public HiveBusinessOperationComposition(
        string operationType,
        HiveBusinessOperationImplementation implementation,
        CorrelationId correlationId,
        string adapterId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationType);

        if (!Enum.IsDefined(implementation))
            throw new ArgumentOutOfRangeException(nameof(implementation));

        if (correlationId == default)
            throw new ArgumentException("Correlation identity is required.", nameof(correlationId));

        ArgumentException.ThrowIfNullOrWhiteSpace(adapterId);

        OperationType = operationType.Trim();
        Implementation = implementation;
        CorrelationId = correlationId;
        AdapterId = adapterId.Trim();
        Stages = implementation switch
        {
            HiveBusinessOperationImplementation.Api =>
                new[] { HiveBusinessOperationImplementation.Api },
            HiveBusinessOperationImplementation.Ui =>
                new[] { HiveBusinessOperationImplementation.Ui },
            HiveBusinessOperationImplementation.ApiAndUi =>
                new[]
                {
                    HiveBusinessOperationImplementation.Api,
                    HiveBusinessOperationImplementation.Ui
                },
            _ => throw new InvalidOperationException("Business-operation implementation is invalid.")
        };
    }

    public string OperationType { get; }

    public HiveBusinessOperationImplementation Implementation { get; }

    public CorrelationId CorrelationId { get; }

    public string AdapterId { get; }

    public IReadOnlyList<HiveBusinessOperationImplementation> Stages { get; }
}

public interface IHiveHostIntegrationAdapter
{
    string AdapterId { get; }

    Task<Result<HiveHostContextDescriptor>> CaptureAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<HiveHostInteractionResult>> ExecuteInteractionAsync(
        HiveHostInteractionRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<HiveLookupOption>>> ResolveLookupAsync(
        HiveLookupRequest request,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}

public interface IHiveHostCapabilityAuthorizer
{
    Result Authorize(
        HiveHostCapabilityRequest request,
        ResourceAccessContext accessContext);
}
