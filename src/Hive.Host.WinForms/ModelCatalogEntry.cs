using Hive.Core;

namespace Hive.Host.WinForms;

/// <summary>
/// Precomputed capability states used by the catalog list columns.
/// </summary>
/// <remarks>
/// The list renders one column per capability. Resolving each column with a
/// LINQ lookup over the discovered capabilities turns one redraw into thousands
/// of scans for a large catalog, so the states are resolved once per snapshot.
/// </remarks>
internal readonly record struct ModelCapabilitySummary(
    CapabilityState Text,
    CapabilityState Vision,
    CapabilityState Tools,
    CapabilityState Structured,
    CapabilityState Reasoning,
    CapabilityState Thinking)
{
    internal static readonly ModelCapabilitySummary Empty = new(
        CapabilityState.Unknown,
        CapabilityState.Unknown,
        CapabilityState.Unknown,
        CapabilityState.Unknown,
        CapabilityState.Unknown,
        CapabilityState.Unknown);

    internal static ModelCapabilitySummary From(ProviderModelMetadata model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.DiscoveredCapabilities.Count == 0)
            return Empty;

        CapabilityState Resolve(CapabilityKey key)
        {
            foreach (var entry in model.DiscoveredCapabilities)
            {
                if (entry.Capability == key)
                    return entry.State;
            }

            return CapabilityState.Unknown;
        }

        return new ModelCapabilitySummary(
            Resolve(HiveCapabilityKeys.TextGeneration),
            Resolve(HiveCapabilityKeys.Vision),
            Resolve(HiveCapabilityKeys.ToolCalling),
            Resolve(HiveCapabilityKeys.StructuredOutput),
            Resolve(HiveCapabilityKeys.Reasoning),
            Resolve(HiveCapabilityKeys.Thinking));
    }
}

/// <summary>
/// One discovered model with its decision-relevant values resolved once.
/// </summary>
internal sealed class ModelCatalogEntry
{
    internal ModelCatalogEntry(
        ProviderModelMetadata model,
        decimal? comparablePrice,
        bool isFree,
        ExecutionTarget? target,
        ModelCapabilitySummary capabilities)
    {
        Model = model;
        ComparablePricePerMillion = comparablePrice;
        IsFree = isFree;
        Target = target;
        Capabilities = capabilities;
    }

    internal ProviderModelMetadata Model { get; }

    internal decimal? ComparablePricePerMillion { get; }

    internal bool IsFree { get; }

    internal ExecutionTarget? Target { get; }

    internal ModelCapabilitySummary Capabilities { get; }

    /// <summary>
    /// Whether the price filter can compare this model's rate at all.
    /// </summary>
    internal bool HasComparablePricing => ComparablePricePerMillion is not null;

    internal string ModelId => Model.ModelId;
}