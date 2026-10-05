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
    ModelCapabilityFilterState CapabilityState);

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

        // No comparable pricing evidence: the model cannot satisfy a bounded
        // price selection, but must not disappear under an unconstrained view.
        return criteria.IsFullPriceRange;
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