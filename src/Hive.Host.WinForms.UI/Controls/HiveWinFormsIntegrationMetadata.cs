using Hive.Core;

namespace Hive.Host.WinForms.UI.Controls;

public interface IHiveWinFormsControl
{
    HiveWinFormsControlMetadata HiveIntegration { get; }
}

public interface IHiveWinFormsFieldControl : IHiveWinFormsControl
{
    HiveWinFormsFieldMetadata HiveField { get; }
}

public interface IHiveWinFormsDataSurface : IHiveWinFormsControl
{
    HiveWinFormsDataSurfaceMetadata HiveDataSurface { get; }
}

public sealed class HiveWinFormsControlMetadata
{
    public string? ControlId { get; set; }
}

public sealed class HiveWinFormsFormMetadata
{
    public string? HostName { get; set; }
}

public sealed class HiveWinFormsFieldMetadata
{
    public string? Name { get; set; }

    public string? BindingMember { get; set; }

    public string? ValueType { get; set; }

    public bool? Required { get; set; }

    public bool? ReadOnly { get; set; }

    public bool? Computed { get; set; }

    public bool? Generated { get; set; }

    public bool? IsPrimaryKey { get; set; }

    public HiveHostLookupDescriptor? Lookup { get; set; }
}

public sealed class HiveWinFormsDataSurfaceMetadata
{
    private readonly Dictionary<string, HiveWinFormsFieldMetadata> _fieldOverrides =
        new(StringComparer.Ordinal);

    private readonly List<HiveHostCapabilityDescriptor> _capabilities = new();

    public string? SurfaceId { get; set; }

    public string? Name { get; set; }

    public string? PrimaryKeyField { get; set; }

    public string? ParentSurfaceId { get; set; }

    public string? ParentKeyField { get; set; }

    public string? ChildKeyField { get; set; }

    public IReadOnlyDictionary<string, HiveWinFormsFieldMetadata> FieldOverrides =>
        _fieldOverrides;

    public IReadOnlyList<HiveHostCapabilityDescriptor> Capabilities =>
        _capabilities;

    public bool HasParentRelationship =>
        !string.IsNullOrWhiteSpace(ParentSurfaceId) ||
        !string.IsNullOrWhiteSpace(ParentKeyField) ||
        !string.IsNullOrWhiteSpace(ChildKeyField);

    public HiveWinFormsFieldMetadata ConfigureField(string fieldKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldKey);

        var key = fieldKey.Trim();

        if (!_fieldOverrides.TryGetValue(key, out var metadata))
        {
            metadata = new HiveWinFormsFieldMetadata();
            _fieldOverrides.Add(key, metadata);
        }

        return metadata;
    }

    public void AddCapability(HiveHostCapabilityDescriptor capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (_capabilities.Any(existing => existing.Id == capability.Id))
        {
            throw new ArgumentException(
                $"Capability '{capability.Id}' is already configured for this data surface.",
                nameof(capability));
        }

        _capabilities.Add(capability);
    }

    public bool TryGetFieldOverride(
        string fieldKey,
        out HiveWinFormsFieldMetadata metadata) =>
        _fieldOverrides.TryGetValue(fieldKey, out metadata!);
}
