using Hive.Core;

namespace Hive.Host.WinForms;

/// <summary>
/// Capability-state selection applied by the Model Information filters.
/// </summary>
internal enum ModelCapabilityFilterState
{
    Any,
    Supported,
    Unsupported,
    Unknown
}

/// <summary>
/// How models with no comparable USD-per-million pricing rate are treated by
/// the price filter.
/// </summary>
/// <remarks>
/// A model can report pricing that Hive cannot compare, for example when the
/// provider was discovered without an established currency or source quantity.
/// Such a model previously disappeared the moment the user bounded the price
/// slider, with nothing on screen explaining why. This makes that state an
/// explicit choice instead of a silent side effect.
/// </remarks>
internal enum UnknownPricingVisibility
{
    /// <summary>Show models with no comparable pricing rate.</summary>
    Include,

    /// <summary>Hide models with no comparable pricing rate.</summary>
    Exclude
}

/// <summary>
/// An immutable snapshot of the Model Information filter selection.
/// </summary>
/// <remarks>
/// This type deliberately carries no reference to any WinForms control. The
/// owning view snapshots live slider/combo state into a criteria value, and the
/// filter rule itself is evaluated against that value alone.
/// </remarks>
internal readonly record struct ModelFilterCriteria(
    decimal MinimumPricePerMillion,
    decimal MaximumPricePerMillion,
    bool IsFullPriceRange,
    CapabilityKey? CapabilityKey,
    ModelCapabilityFilterState CapabilityState,
    UnknownPricingVisibility UnknownPricing = UnknownPricingVisibility.Exclude);

/// <summary>
/// The single owner of the Model Information filter rule.
/// </summary>
/// <remarks>
/// <para>
/// Price and free-model semantics previously lived inside
/// <c>HiveModelInformationSettingsView</c> entangled with live control state and
/// detail rendering, which made the rule impossible to test directly. This type
/// is pure: it has no control, form, or ambient-state dependency, so the rule can
/// be verified in isolation.
/// </para>
/// <para>
/// The free-model rule is owned here and nowhere else. Presentation reads
/// <see cref="IsFreeModel"/> rather than re-deriving free-ness from
/// <c>ExplicitFreeEvidence</c> independently, so filtering and rendering cannot
/// disagree about the same model.
/// </para>
/// </remarks>
internal static class ModelInformationFilter
{
    /// <summary>
    /// Evaluates whether a discovered model satisfies the supplied criteria.
    /// </summary>
    public static bool Matches(
        ProviderModelMetadata model,
        ModelFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(model);

        return MatchesPrice(model, criteria) &&
               MatchesCapability(model, criteria);
    }

    /// <summary>
    /// Evaluates the filter against an entry whose pricing and capability state
    /// were already resolved.
    /// </summary>
    /// <remarks>
    /// This overload exists so filtering a large catalog does not recompute the
    /// comparable per-million price for every model on every slider tick.
    /// </remarks>
    public static bool Matches(
        ModelCatalogEntry entry,
        ModelFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return MatchesPrice(entry, criteria) &&
               MatchesCapability(entry, criteria);
    }

    /// <summary>
    /// The filter's comparable token price, in USD per million tokens, or
    /// <c>null</c> when the model has no comparable pricing evidence.
    /// </summary>
    /// <remarks>
    /// Missing pricing is never interpreted as free. Only explicit provider
    /// evidence, or an explicit-free profile that carries no comparable
    /// input/output token rate at all, resolves to a comparable zero.
    /// </remarks>
    public static decimal? GetComparableTokenPricePerMillion(
        ProviderModelMetadata model)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Pricing?.TryGetComparableTokenPricePerMillion()
            ?? (
                model.Pricing?.ExplicitFreeEvidence == true &&
                model.Pricing.Prices.All(
                    static price =>
                        !string.Equals(
                            price.BillingUnit,
                            "input_token",
                            StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(
                            price.BillingUnit,
                            "output_token",
                            StringComparison.OrdinalIgnoreCase))
                    ? 0m
                    : null);
    }

    /// <summary>
    /// The single authority for whether a model is free for filtering purposes.
    /// </summary>
    public static bool IsFreeModel(
        ProviderModelMetadata model,
        decimal? comparablePrice)
    {
        ArgumentNullException.ThrowIfNull(model);

        return model.Pricing?.ExplicitFreeEvidence == true ||
               model.Pricing?.HasZeroComparableInputOutputTokenPricing == true ||
               comparablePrice == 0m;
    }

        private static bool MatchesPrice(
        ModelCatalogEntry entry,
        ModelFilterCriteria criteria)
    {
        var comparablePrice = entry.ComparablePricePerMillion;

        if (criteria.MaximumPricePerMillion == 0m)
            return entry.IsFree;

        if (comparablePrice is not null)
            return comparablePrice.Value >= criteria.MinimumPricePerMillion &&
                   comparablePrice.Value <= criteria.MaximumPricePerMillion;

        return criteria.UnknownPricing == UnknownPricingVisibility.Include ||
               criteria.IsFullPriceRange;
    }

    private static bool MatchesCapability(
        ModelCatalogEntry entry,
        ModelFilterCriteria criteria)
    {
        if (criteria.CapabilityKey is not { } capabilityKey)
            return true;

        if (criteria.CapabilityState == ModelCapabilityFilterState.Any)
            return true;

        var discoveredState = entry.Capabilities.Resolve(capabilityKey);

        return criteria.CapabilityState switch
        {
            ModelCapabilityFilterState.Supported =>
                discoveredState == CapabilityState.Supported,
            ModelCapabilityFilterState.Unsupported =>
                discoveredState == CapabilityState.Unsupported,
            ModelCapabilityFilterState.Unknown =>
                discoveredState == CapabilityState.Unknown,
            _ => true
        };
    }

    /// <summary>
    /// Resolves a capability key against the precomputed summary.
    /// </summary>
    internal static CapabilityState Resolve(
        this ModelCapabilitySummary summary,
        CapabilityKey capabilityKey)
    {
        if (capabilityKey == HiveCapabilityKeys.TextGeneration)
            return summary.Text;

        if (capabilityKey == HiveCapabilityKeys.Vision)
            return summary.Vision;

        if (capabilityKey == HiveCapabilityKeys.ToolCalling)
            return summary.Tools;

        if (capabilityKey == HiveCapabilityKeys.StructuredOutput)
            return summary.Structured;

        if (capabilityKey == HiveCapabilityKeys.Reasoning)
            return summary.Reasoning;

        if (capabilityKey == HiveCapabilityKeys.Thinking)
            return summary.Thinking;

        return CapabilityState.Unknown;
    }

    private static bool MatchesPrice(
        ProviderModelMetadata model,
        ModelFilterCriteria criteria)
    {
        var comparablePrice = GetComparableTokenPricePerMillion(model);

        // A zero maximum is the explicit "show free models only" view. It is
        // selected by the user, so it does not require the slider to be at its
        // full-range position.
        if (criteria.MaximumPricePerMillion == 0m)
            return IsFreeModel(model, comparablePrice);

        if (comparablePrice is not null)
            return comparablePrice.Value >= criteria.MinimumPricePerMillion &&
                   comparablePrice.Value <= criteria.MaximumPricePerMillion;

        // No comparable pricing evidence. The model cannot satisfy a numeric
        // price comparison, so whether it is shown is governed by the explicit
        // visibility choice rather than by the slider position. Previously a
        // null rate meant "hide" under any bounded range, which made the whole
        // catalog vanish with no explanation.
        return criteria.UnknownPricing == UnknownPricingVisibility.Include ||
               criteria.IsFullPriceRange;
    }

    /// <summary>
    /// Computes the price-slider ceiling for a snapshot.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Real provider catalogs are heavily skewed. OpenRouter spans roughly
    /// $0.01/M to $600/M, with the overwhelming majority of models at the cheap
    /// end. A ceiling set by the single most expensive model therefore produces
    /// a slider whose entire usable range is crushed into a fraction of a
    /// percent of its travel, which is not a filter anyone can operate.
    /// </para>
    /// <para>
    /// The ceiling is instead taken from a high percentile and rounded up to a
    /// friendly increment. Models above the ceiling remain reachable through the
    /// explicit "show above range" control, and the view reports how many they
    /// are, so nothing is silently unreachable.
    /// </para>
    /// </remarks>
    public static decimal CalculatePriceCeiling(
        IReadOnlyList<decimal> comparablePrices)
    {
        ArgumentNullException.ThrowIfNull(comparablePrices);

        if (comparablePrices.Count == 0)
            return DefaultCeilingWithoutComparablePricing;

        var ordered = comparablePrices
            .Where(static price => price > 0m)
            .Order()
            .ToArray();

        if (ordered.Length == 0)
            return DefaultCeilingWithoutComparablePricing;

        // Index at the target percentile, inclusive of the top of that band.
        var index = (int)Math.Ceiling(
            ordered.Length * CeilingPercentile) - 1;

        if (index < 0)
            index = 0;

        if (index > ordered.Length - 1)
            index = ordered.Length - 1;

        var anchor = ordered[index];

        var increment =
            anchor switch
            {
                < 0.01m => 0.01m,
                < 0.05m => 0.05m,
                < 0.10m => 0.10m,
                < 0.25m => 0.25m,
                < 0.50m => 0.50m,
                < 1m => 1m,
                < 5m => 5m,
                < 10m => 10m,
                < 25m => 25m,
                < 50m => 50m,
                < 100m => 100m,
                _ => RoundIncrement(anchor)
            };

        var ceiling = Math.Ceiling(anchor / increment) * increment;

        return ceiling < anchor ? anchor : ceiling;
    }

    /// <summary>
    /// Counts comparable rates above the active ceiling.
    /// </summary>
    public static int CountAboveCeiling(
        IReadOnlyList<decimal> comparablePrices,
        decimal ceiling)
    {
        ArgumentNullException.ThrowIfNull(comparablePrices);

        return comparablePrices.Count(price => price > ceiling);
    }

    private static decimal RoundIncrement(decimal anchor)
    {
        var magnitude = (decimal)Math.Pow(
            10,
            Math.Floor(Math.Log10((double)anchor)) - 1);

        return Math.Max(magnitude, 1m);
    }

    private const decimal DefaultCeilingWithoutComparablePricing = 1m;
    private const decimal CeilingPercentile = 0.90m;

    /// <summary>
    /// Counts how many models carry no comparable pricing rate.
    /// </summary>
    /// <remarks>
    /// The view uses this to explain, rather than silently apply, a bounded
    /// price selection.
    /// </remarks>
    public static int CountWithoutComparablePricing(
        IEnumerable<ProviderModelMetadata> models)
    {
        ArgumentNullException.ThrowIfNull(models);

        return models.Count(
            static model =>
                GetComparableTokenPricePerMillion(model) is null &&
                !IsFreeModel(model, null));
    }

    private static bool MatchesCapability(
        ProviderModelMetadata model,
        ModelFilterCriteria criteria)
    {
        if (criteria.CapabilityKey is not { } capabilityKey)
            return true;

        if (criteria.CapabilityState == ModelCapabilityFilterState.Any)
            return true;

        var discoveredState = model.DiscoveredCapabilities
            .FirstOrDefault(entry => entry.Capability == capabilityKey)
            ?.State;

        return criteria.CapabilityState switch
        {
            ModelCapabilityFilterState.Supported =>
                discoveredState == CapabilityState.Supported,
            ModelCapabilityFilterState.Unsupported =>
                discoveredState == CapabilityState.Unsupported,
            ModelCapabilityFilterState.Unknown =>
                discoveredState == CapabilityState.Unknown ||
                discoveredState is null,
            _ => true
        };
    }
}