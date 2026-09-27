using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using System.ComponentModel;

namespace Hive.Host.WinForms;

internal sealed record HiveWinFormsDataSurfaceCaptureEntry(
    DataGridView Grid,
    HiveWinFormsDataSurfaceMetadata? Metadata,
    HiveHostDataSurfaceDescriptor Surface,
    string Path);

internal sealed class HiveWinFormsDataSurfaceDescriptorBuilder
{
    public HiveHostDataSurfaceDescriptor Create(
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
            HiveWinFormsCapabilityIdentity.Create(
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
            HiveWinFormsText.CleanRequired(
                metadata?.Name,
                string.IsNullOrWhiteSpace(grid.Name)
                    ? "WinForms data surface"
                    : grid.Name),
            TryGetBoundRowCount(grid),
            fields,
            capabilities);
    }

    public static List<HiveHostDataSurfaceDescriptor> ApplyParentChildRelationships(
        IReadOnlyList<HiveWinFormsDataSurfaceCaptureEntry> entries)
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

            var parentSurfaceId = HiveWinFormsText.CleanOptional(metadata.ParentSurfaceId);
            var parentKeyField = HiveWinFormsText.CleanOptional(metadata.ParentKeyField);
            var childKeyField = HiveWinFormsText.CleanOptional(metadata.ChildKeyField);

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
            try
            {
                var dataSource = grid.DataSource;
                var bindingContext = grid.BindingContext;

                if (dataSource is not null && bindingContext is not null)
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
            }
            catch (ArgumentException)
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.binding-inspection-failed",
                    "The data-surface binding could not be inspected.");
            }
            catch (InvalidOperationException)
            {
                throw new HiveWinFormsIntegrationException(
                    "hive.host.winforms.binding-inspection-failed",
                    "The data-surface binding could not be inspected.");
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
        var name = HiveWinFormsText.CleanRequired(
            metadata?.Name,
            string.IsNullOrWhiteSpace(fieldKey)
                ? column.Name
                : fieldKey);

        var bindingMember =
            HiveWinFormsText.CleanOptional(metadata?.BindingMember) ??
            HiveWinFormsText.CleanOptional(column.DataPropertyName);

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
            HiveWinFormsText.CleanRequired(metadata?.Name, propertyName),
            HiveWinFormsText.CleanOptional(metadata?.BindingMember) ?? propertyName,
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
            HiveWinFormsText.CleanRequired(
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

    private static int TryGetBoundRowCount(DataGridView grid)
    {
        try
        {
            var dataSource = grid.DataSource;

            if (dataSource is not null)
            {
                var bindingContext = grid.BindingContext;
                if (bindingContext is not null)
                {
                    var manager = bindingContext[
                        dataSource,
                        grid.DataMember];

                    if (manager is not null)
                        return Math.Max(0, manager.Count);
                }
            }

            return grid.AllowUserToAddRows
                ? Math.Max(0, grid.Rows.Count - 1)
                : grid.Rows.Count;
        }
        catch (ArgumentException)
        {
            throw new HiveWinFormsIntegrationException(
                "hive.host.winforms.binding-row-count-failed",
                "The data-surface row count could not be determined from its binding.");
        }
        catch (InvalidOperationException)
        {
            throw new HiveWinFormsIntegrationException(
                "hive.host.winforms.binding-row-count-failed",
                "The data-surface row count could not be determined from its binding.");
        }
    }
}
