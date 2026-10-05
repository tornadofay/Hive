using Hive.Core;

namespace Hive.Core;

public sealed record ExecutionPricingEvidence
{
    public ExecutionPricingEvidence(
        string modelId,
        ProviderModelPricing pricing,
        DateTimeOffset observedAtUtc,
        DateTimeOffset staleAfterUtc)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException(
                "Execution pricing model identity is required.",
                nameof(modelId));

        var normalizedModelId = modelId.Trim();

        if (normalizedModelId.Length > 512)
            throw new ArgumentException(
                "Execution pricing model identity cannot exceed 512 characters.",
                nameof(modelId));

        ArgumentNullException.ThrowIfNull(pricing);

        observedAtUtc = observedAtUtc.ToUniversalTime();
        staleAfterUtc = staleAfterUtc.ToUniversalTime();

        if (staleAfterUtc <= observedAtUtc)
            throw new ArgumentException(
                "Execution pricing evidence must become stale after it was observed.",
                nameof(staleAfterUtc));

        ModelId = normalizedModelId;
        Pricing = pricing;
        ObservedAtUtc = observedAtUtc;
        StaleAfterUtc = staleAfterUtc;
    }

    public string ModelId { get; }

    public ProviderModelPricing Pricing { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public DateTimeOffset StaleAfterUtc { get; }

    public bool IsStale(DateTimeOffset nowUtc) =>
        nowUtc.ToUniversalTime() >= StaleAfterUtc;
}
