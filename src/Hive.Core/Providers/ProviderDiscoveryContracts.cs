using System.Collections.ObjectModel;

namespace Hive.Core;

public enum ProviderDiscoveryState
{
    Supported,
    Unsupported,
    Unknown
}

public enum ProviderAvailabilityStatus
{
    Unknown,
    Available,
    Unavailable
}

public enum ProviderHealthStatus
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

public sealed record ProviderOperationalMetadata
{
    public ProviderOperationalMetadata(
        ProviderAvailabilityStatus availability,
        ProviderHealthStatus health,
        DateTimeOffset observedAtUtc,
        DateTimeOffset staleAfterUtc,
        int? rateLimitRemaining = null)
    {
        if (!Enum.IsDefined(availability))
            throw new ArgumentOutOfRangeException(nameof(availability), availability, "Provider availability status is invalid.");

        if (!Enum.IsDefined(health))
            throw new ArgumentOutOfRangeException(nameof(health), health, "Provider health status is invalid.");

        observedAtUtc = observedAtUtc.ToUniversalTime();
        staleAfterUtc = staleAfterUtc.ToUniversalTime();

        if (staleAfterUtc <= observedAtUtc)
        {
            throw new ArgumentException(
                "Discovery metadata must become stale after it was observed.",
                nameof(staleAfterUtc));
        }

        if (rateLimitRemaining is < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rateLimitRemaining),
                rateLimitRemaining,
                "Rate-limit remaining cannot be negative.");
        }

        Availability = availability;
        Health = health;
        ObservedAtUtc = observedAtUtc;
        StaleAfterUtc = staleAfterUtc;
        RateLimitRemaining = rateLimitRemaining;
    }

    public ProviderAvailabilityStatus Availability { get; }

    public ProviderHealthStatus Health { get; }

    public DateTimeOffset ObservedAtUtc { get; }

    public DateTimeOffset StaleAfterUtc { get; }

    public int? RateLimitRemaining { get; }

    public bool IsStale(DateTimeOffset nowUtc) =>
        nowUtc.ToUniversalTime() >= StaleAfterUtc;
}

public sealed record ProviderModelMetadata
{
    public ProviderModelMetadata(
        string modelId,
        string? ownedBy,
        DateTimeOffset? createdAtUtc,
        ProviderAvailabilityStatus availability,
        ProviderHealthStatus health,
        IReadOnlyList<CapabilityStateEntry> discoveredCapabilities)
    {
        if (string.IsNullOrWhiteSpace(modelId))
            throw new ArgumentException("Model identity is required.", nameof(modelId));

        var normalizedModelId = modelId.Trim();

        if (normalizedModelId.Length > 512)
            throw new ArgumentException("Model identity cannot exceed 512 characters.", nameof(modelId));

        if (!Enum.IsDefined(availability))
            throw new ArgumentOutOfRangeException(nameof(availability), availability, "Model availability status is invalid.");

        if (!Enum.IsDefined(health))
            throw new ArgumentOutOfRangeException(nameof(health), health, "Model health status is invalid.");

        if (ownedBy is not null)
        {
            if (string.IsNullOrWhiteSpace(ownedBy))
                throw new ArgumentException("Model owner cannot be empty when supplied.", nameof(ownedBy));

            ownedBy = ownedBy.Trim();

            if (ownedBy.Length > 200)
                throw new ArgumentException("Model owner cannot exceed 200 characters.", nameof(ownedBy));
        }

        if (createdAtUtc is { } created)
            createdAtUtc = created.ToUniversalTime();

        ArgumentNullException.ThrowIfNull(discoveredCapabilities);

        var capabilities = new List<CapabilityStateEntry>(discoveredCapabilities.Count);
        var keys = new HashSet<CapabilityKey>();

        foreach (var capability in discoveredCapabilities)
        {
            ArgumentNullException.ThrowIfNull(capability);

            if (!keys.Add(capability.Capability))
            {
                throw new ArgumentException(
                    $"Duplicate discovered capability '{capability.Capability}' is not allowed.",
                    nameof(discoveredCapabilities));
            }

            capabilities.Add(capability);
        }

        ModelId = normalizedModelId;
        OwnedBy = ownedBy;
        CreatedAtUtc = createdAtUtc;
        Availability = availability;
        Health = health;
        DiscoveredCapabilities = new ReadOnlyCollection<CapabilityStateEntry>(capabilities);
    }

    public string ModelId { get; }

    public string? OwnedBy { get; }

    public DateTimeOffset? CreatedAtUtc { get; }

    public ProviderAvailabilityStatus Availability { get; }

    public ProviderHealthStatus Health { get; }

    public IReadOnlyList<CapabilityStateEntry> DiscoveredCapabilities { get; }
}

public sealed record ProviderDiscoverySnapshot
{
    public ProviderDiscoverySnapshot(
        ProviderId providerId,
        ProviderAccountId providerAccountId,
        Uri endpoint,
        ProviderOperationalMetadata operational,
        ProviderDiscoveryState modelEnumerationState,
        IReadOnlyList<ProviderModelMetadata> models)
    {
        if (providerId == default)
            throw new ArgumentException("Provider identity is required.", nameof(providerId));

        if (providerAccountId == default)
            throw new ArgumentException("Provider account identity is required.", nameof(providerAccountId));

        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttp &&
             endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Discovery endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
            throw new ArgumentException(
                "Discovery endpoints must not embed credentials.",
                nameof(endpoint));

        ArgumentNullException.ThrowIfNull(operational);

        if (!Enum.IsDefined(modelEnumerationState))
            throw new ArgumentOutOfRangeException(
                nameof(modelEnumerationState),
                modelEnumerationState,
                "Provider discovery state is invalid.");

        ArgumentNullException.ThrowIfNull(models);

        var modelList = new List<ProviderModelMetadata>(models.Count);
        var modelIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var model in models)
        {
            ArgumentNullException.ThrowIfNull(model);

            if (!modelIds.Add(model.ModelId))
            {
                throw new ArgumentException(
                    $"Duplicate discovered model '{model.ModelId}' is not allowed.",
                    nameof(models));
            }

            modelList.Add(model);
        }

        ProviderId = providerId;
        ProviderAccountId = providerAccountId;
        Endpoint = endpoint;
        Operational = operational;
        ModelEnumerationState = modelEnumerationState;
        Models = new ReadOnlyCollection<ProviderModelMetadata>(modelList);
    }

    public ProviderId ProviderId { get; }

    public ProviderAccountId ProviderAccountId { get; }

    public Uri Endpoint { get; }

    public ProviderOperationalMetadata Operational { get; }

    public ProviderDiscoveryState ModelEnumerationState { get; }

    public IReadOnlyList<ProviderModelMetadata> Models { get; }

    public bool IsStale(DateTimeOffset nowUtc) =>
        Operational.IsStale(nowUtc);
}

public interface IProviderCapabilityDiscovery
{
    Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
        Provider provider,
        ProviderAccount account,
        ExecutionTarget target,
        SecretMaterial? credential,
        CancellationToken cancellationToken = default);
}

public static class ExecutionTargetCapabilityResolver
{
    public static IReadOnlyList<CapabilityStateEntry> ResolveCapabilities(
        ExecutionTarget target,
        ProviderDiscoverySnapshot? discovery,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (discovery is null ||
            discovery.IsStale(nowUtc) ||
            discovery.ProviderId != target.ProviderId ||
            discovery.ProviderAccountId != target.ProviderAccountId ||
            !Uri.Compare(
                discovery.Endpoint,
                target.Endpoint,
                UriComponents.AbsoluteUri,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase).Equals(0))
        {
            return target.Capabilities;
        }

        var modelId = target.Model ?? target.Deployment;
        if (modelId is null)
            return target.Capabilities;

        var model = discovery.Models.FirstOrDefault(
            candidate => string.Equals(
                candidate.ModelId,
                modelId,
                StringComparison.Ordinal));

        if (model is null)
            return target.Capabilities;

        var effective = target.Capabilities.ToDictionary(
            capability => capability.Capability,
            capability => capability.State);

        foreach (var discovered in model.DiscoveredCapabilities)
        {
            if (!effective.ContainsKey(discovered.Capability))
                effective.Add(
                    discovered.Capability,
                    discovered.State);
        }

        var result = target.Capabilities.ToList();

        foreach (var discovered in model.DiscoveredCapabilities)
        {
            if (target.Capabilities.Any(
                    configured => configured.Capability == discovered.Capability))
            {
                continue;
            }

            result.Add(discovered);
        }

        return new ReadOnlyCollection<CapabilityStateEntry>(result);
    }
}
