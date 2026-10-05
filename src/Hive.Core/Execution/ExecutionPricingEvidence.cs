namespace Hive.Core;

public sealed record ExecutionPricingEvidence
{
    public ExecutionPricingEvidence(
        ProviderId providerId,
        ProviderAccountId providerAccountId,
        Uri endpoint,
        string modelId,
        ProviderModelPricing pricing,
        DateTimeOffset observedAtUtc,
        DateTimeOffset staleAfterUtc)
    {
        if (providerId == default)
        {
            throw new ArgumentException(
                "Execution pricing Provider identity is required.",
                nameof(providerId));
        }

        if (providerAccountId == default)
        {
            throw new ArgumentException(
                "Execution pricing ProviderAccount identity is required.",
                nameof(providerAccountId));
        }

        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttp &&
             endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Execution pricing evidence endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException(
                "Execution pricing evidence endpoints must not embed credentials.",
                nameof(endpoint));
        }

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

        ProviderId = providerId;
        ProviderAccountId = providerAccountId;
        Endpoint = endpoint;
        ModelId = normalizedModelId;
        Pricing = pricing;
        ObservedAtUtc = observedAtUtc;
        StaleAfterUtc = staleAfterUtc;
    }

    public ProviderId ProviderId { get; }

    public ProviderAccountId ProviderAccountId { get; }

    public Uri Endpoint { get; }

    public string ModelId { get; }

    public ProviderModelPricing Pricing { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public DateTimeOffset StaleAfterUtc { get; }

    public bool IsStale(DateTimeOffset nowUtc) =>
        nowUtc.ToUniversalTime() >= StaleAfterUtc;
}
