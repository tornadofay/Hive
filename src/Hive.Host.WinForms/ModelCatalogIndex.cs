using Hive.Core;

namespace Hive.Host.WinForms;

/// <summary>
/// An immutable, precomputed view over one discovery snapshot.
/// </summary>
/// <remarks>
/// <para>
/// Filtering a large catalog on every slider tick used to repeat expensive work
/// per model: the comparable per-million price was recomputed from the pricing
/// entries, and the matching execution target was found with a linear scan over
/// all targets, for every model, on every redraw. The list columns added
/// thousands of further capability scans.
/// </para>
/// <para>
/// This index resolves all of that once per snapshot, so a filter tick is a
/// comparison over cached decimals plus a dictionary lookup.
/// </para>
/// </remarks>
internal sealed class ModelCatalogIndex
{
    private readonly Dictionary<(string Endpoint, string ModelId), ExecutionTarget>
        _targetsByEndpointAndModel;

    private ModelCatalogIndex(
        ModelCatalogEntry[] entries,
        decimal[] comparablePrices,
        decimal highestComparablePrice,
        int withoutComparablePricingCount,
        Dictionary<(string, string), ExecutionTarget> targets)
    {
        Entries = entries;
        ComparablePrices = comparablePrices;
        HighestComparablePrice = highestComparablePrice;
        WithoutComparablePricingCount = withoutComparablePricingCount;
        _targetsByEndpointAndModel = targets;
    }

    internal ModelCatalogEntry[] Entries { get; }

    internal decimal[] ComparablePrices { get; }

    internal decimal HighestComparablePrice { get; }

    internal int WithoutComparablePricingCount { get; }

    internal static ModelCatalogIndex Build(
        IReadOnlyList<ProviderModelMetadata> models,
        IReadOnlyList<ExecutionTarget> executionTargets,
        Uri? selectedEndpoint)
    {
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(executionTargets);

        var targets = BuildTargetLookup(executionTargets, selectedEndpoint);

        var entries = new ModelCatalogEntry[models.Count];
        var prices = new List<decimal>(models.Count);
        var withoutComparablePricing = 0;
        var highest = 0m;

        for (var index = 0; index < models.Count; index++)
        {
            var model = models[index];
            var comparable =
                ModelInformationFilter.GetComparableTokenPricePerMillion(model);
            var isFree = ModelInformationFilter.IsFreeModel(model, comparable);

            entries[index] = new ModelCatalogEntry(
                model,
                comparable,
                isFree,
                ResolveTarget(targets, selectedEndpoint, model),
                ModelCapabilitySummary.From(model));

            if (comparable is null)
            {
                if (!isFree)
                    withoutComparablePricing++;
            }
            else
            {
                prices.Add(comparable.Value);

                if (comparable.Value > highest)
                    highest = comparable.Value;
            }
        }

        return new ModelCatalogIndex(
            entries,
            prices.ToArray(),
            highest,
            withoutComparablePricing,
            targets);
    }

    /// <summary>
    /// Selects the entries matching the supplied criteria.
    /// </summary>
    internal List<ModelCatalogEntry> Select(ModelFilterCriteria criteria)
    {
        var result = new List<ModelCatalogEntry>(Entries.Length);

        foreach (var entry in Entries)
        {
            if (ModelInformationFilter.Matches(entry, criteria))
                result.Add(entry);
        }

        return result;
    }

    /// <summary>
    /// Counts matching entries above the active ceiling.
    /// </summary>
    internal int CountAboveCeiling(decimal ceiling)
    {
        if (ceiling <= 0m)
            return 0;

        var count = 0;

        foreach (var price in ComparablePrices)
        {
            if (price > ceiling)
                count++;
        }

        return count;
    }

    private static Dictionary<(string, string), ExecutionTarget> BuildTargetLookup(
        IReadOnlyList<ExecutionTarget> executionTargets,
        Uri? selectedEndpoint)
    {
        var lookup = new Dictionary<(string, string), ExecutionTarget>(
            executionTargets.Count);

        if (selectedEndpoint is null)
            return lookup;

        var endpointKey = NormalizeEndpoint(selectedEndpoint);

        foreach (var target in executionTargets)
        {
            if (!string.Equals(
                    NormalizeEndpoint(target.Endpoint),
                    endpointKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var modelId in new[] { target.Model, target.Deployment })
            {
                if (string.IsNullOrWhiteSpace(modelId))
                    continue;

                lookup.TryAdd(
                    (endpointKey, modelId.Trim().ToLowerInvariant()),
                    target);
            }
        }

        return lookup;
    }

    private static ExecutionTarget? ResolveTarget(
        Dictionary<(string, string), ExecutionTarget> targets,
        Uri? selectedEndpoint,
        ProviderModelMetadata model)
    {
        if (selectedEndpoint is null)
            return null;

        return targets.TryGetValue(
                (NormalizeEndpoint(selectedEndpoint),
                 model.ModelId.ToLowerInvariant()),
                out var target)
            ? target
            : null;
    }

    private static string NormalizeEndpoint(Uri endpoint) =>
        endpoint.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
}