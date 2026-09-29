using System.Collections.ObjectModel;
using System.Text.Json;

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

public static class HiveCapabilityKeys
{
    public static CapabilityKey TextGeneration => new("text.generate");

    public static CapabilityKey Vision => new("vision");

    public static CapabilityKey ToolCalling => new("tool.calling");

    public static CapabilityKey StructuredOutput => new("structured.output");

    public static CapabilityKey Reasoning => new("reasoning");

    public static CapabilityKey Thinking => new("thinking");
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

public sealed record ProviderModelLimits
{
    private const int MaxAdditionalConstraintCount = 32;
    private const int MaxAdditionalConstraintKeyLength = 128;
    private const int MaxAdditionalConstraintBytes = 16 * 1024;
    private const int MaxAdditionalConstraintTotalBytes = 32 * 1024;

    public ProviderModelLimits(
        long? contextWindowTokens = null,
        long? maxInputTokens = null,
        long? maxOutputTokens = null,
        IReadOnlyDictionary<string, JsonElement>? additionalConstraints = null)
    {
        ValidateLimit(contextWindowTokens, nameof(contextWindowTokens));
        ValidateLimit(maxInputTokens, nameof(maxInputTokens));
        ValidateLimit(maxOutputTokens, nameof(maxOutputTokens));

        ArgumentNullException.ThrowIfNull(additionalConstraints);

        if (additionalConstraints.Count > MaxAdditionalConstraintCount)
        {
            throw new ArgumentException(
                $"Additional model constraints cannot exceed {MaxAdditionalConstraintCount} entries.",
                nameof(additionalConstraints));
        }

        var normalized = new Dictionary<string, JsonElement>(
            StringComparer.Ordinal);
        var totalBytes = 0;

        foreach (var pair in additionalConstraints)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException(
                    "Additional model constraint keys are required.",
                    nameof(additionalConstraints));

            var key = pair.Key.Trim();
            if (key.Length > MaxAdditionalConstraintKeyLength)
            {
                throw new ArgumentException(
                    $"Additional model constraint keys cannot exceed {MaxAdditionalConstraintKeyLength} characters.",
                    nameof(additionalConstraints));
            }

            if (pair.Value.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException(
                    $"Additional model constraint '{key}' has an invalid JSON value.",
                    nameof(additionalConstraints));
            }

            var rawText = pair.Value.GetRawText();
            if (rawText.Length > MaxAdditionalConstraintBytes)
            {
                throw new ArgumentException(
                    $"Additional model constraint '{key}' exceeds the bounded value size.",
                    nameof(additionalConstraints));
            }

            totalBytes += rawText.Length;
            if (totalBytes > MaxAdditionalConstraintTotalBytes)
            {
                throw new ArgumentException(
                    "Additional model constraints exceed the bounded aggregate size.",
                    nameof(additionalConstraints));
            }

            if (!normalized.TryAdd(key, pair.Value.Clone()))
            {
                throw new ArgumentException(
                    $"Duplicate additional model constraint '{key}' is not allowed.",
                    nameof(additionalConstraints));
            }
        }

        ContextWindowTokens = contextWindowTokens;
        MaxInputTokens = maxInputTokens;
        MaxOutputTokens = maxOutputTokens;
        AdditionalConstraints = new ReadOnlyDictionary<string, JsonElement>(normalized);
    }

    public long? ContextWindowTokens { get; }

    public long? MaxInputTokens { get; }

    public long? MaxOutputTokens { get; }

    public IReadOnlyDictionary<string, JsonElement> AdditionalConstraints { get; }

    private static void ValidateLimit(long? value, string parameterName)
    {
        if (value is < 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Model limits cannot be negative.");
        }
    }
}

public sealed record ProviderModelPrice
{
    public ProviderModelPrice(
        string billingUnit,
        decimal price,
        string? currency = null,
        decimal? unitQuantity = null)
    {
        if (string.IsNullOrWhiteSpace(billingUnit))
            throw new ArgumentException("Model billing unit is required.", nameof(billingUnit));

        BillingUnit = billingUnit.Trim();
        if (BillingUnit.Length > 128)
            throw new ArgumentException(
                "Model billing unit cannot exceed 128 characters.",
                nameof(billingUnit));

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(price),
                price,
                "Model pricing cannot be negative.");
        }

        if (currency is not null)
        {
            currency = currency.Trim();
            if (currency.Length == 0)
                currency = null;
            else if (currency.Length > 16)
                throw new ArgumentException(
                    "Model pricing currency cannot exceed 16 characters.",
                    nameof(currency));
        }

        if (unitQuantity is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(unitQuantity),
                unitQuantity,
                "Pricing unit quantity must be greater than zero.");
        }

        Price = price;
        Currency = currency;
        UnitQuantity = unitQuantity;
    }

    public string BillingUnit { get; }

    public decimal Price { get; }

    public string? Currency { get; }

    public decimal? UnitQuantity { get; }
}

public sealed record ProviderModelPricing
{
    private const int MaxPrices = 32;

    public ProviderModelPricing(
        IReadOnlyList<ProviderModelPrice> prices,
        bool explicitFreeEvidence = false)
    {
        ArgumentNullException.ThrowIfNull(prices);

        if (prices.Count > MaxPrices)
        {
            throw new ArgumentException(
                $"A model pricing profile cannot contain more than {MaxPrices} entries.",
                nameof(prices));
        }

        var normalized = new List<ProviderModelPrice>(prices.Count);
        var units = new HashSet<string>(StringComparer.Ordinal);

        foreach (var price in prices)
        {
            ArgumentNullException.ThrowIfNull(price);

            if (!units.Add(price.BillingUnit))
            {
                throw new ArgumentException(
                    $"Duplicate model pricing unit '{price.BillingUnit}' is not allowed.",
                    nameof(prices));
            }

            normalized.Add(price);
        }

        Prices = new ReadOnlyCollection<ProviderModelPrice>(normalized);
        ExplicitFreeEvidence = explicitFreeEvidence;
    }

    public IReadOnlyList<ProviderModelPrice> Prices { get; }

    public bool ExplicitFreeEvidence { get; }
}

public sealed record ProviderModelMetadata
{
    private const int MaxModalityCount = 32;
    private const int MaxModalityLength = 64;
    private const int MaxThinkingOptionCount = 32;
    private const int MaxThinkingOptionLength = 128;
    private const int MaxExtensionPropertyCount = 128;
    private const int MaxExtensionKeyLength = 128;
    private const int MaxExtensionPropertyBytes = 16 * 1024;
    private const int MaxExtensionTotalBytes = 64 * 1024;

    public ProviderModelMetadata(
        string modelId,
        string? ownedBy,
        DateTimeOffset? createdAtUtc,
        ProviderAvailabilityStatus availability,
        ProviderHealthStatus health,
        IReadOnlyList<CapabilityStateEntry> discoveredCapabilities,
        IReadOnlyList<string>? inputModalities = null,
        IReadOnlyList<string>? outputModalities = null,
        IReadOnlyList<string>? thinkingOptions = null,
        string? defaultThinkingLevel = null,
        ProviderModelLimits? limits = null,
        ProviderModelPricing? pricing = null,
        IReadOnlyDictionary<string, JsonElement>? extensionData = null,
        DateTimeOffset? observedAtUtc = null,
        DateTimeOffset? staleAfterUtc = null)
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

        InputModalities = NormalizeStrings(
            inputModalities,
            MaxModalityCount,
            MaxModalityLength,
            nameof(inputModalities));

        OutputModalities = NormalizeStrings(
            outputModalities,
            MaxModalityCount,
            MaxModalityLength,
            nameof(outputModalities));

        ThinkingOptions = NormalizeStrings(
            thinkingOptions,
            MaxThinkingOptionCount,
            MaxThinkingOptionLength,
            nameof(thinkingOptions));

        if (defaultThinkingLevel is not null)
        {
            if (string.IsNullOrWhiteSpace(defaultThinkingLevel))
            {
                throw new ArgumentException(
                    "Default thinking level cannot be empty when supplied.",
                    nameof(defaultThinkingLevel));
            }

            defaultThinkingLevel = defaultThinkingLevel.Trim();
            if (defaultThinkingLevel.Length > MaxThinkingOptionLength)
            {
                throw new ArgumentException(
                    "Default thinking level cannot exceed 128 characters.",
                    nameof(defaultThinkingLevel));
            }
        }

        if ((observedAtUtc is null) != (staleAfterUtc is null))
        {
            throw new ArgumentException(
                "Model observation freshness requires both observed and stale timestamps.",
                nameof(observedAtUtc));
        }

        if (observedAtUtc is { } observed)
            observedAtUtc = observed.ToUniversalTime();

        if (staleAfterUtc is { } stale)
            staleAfterUtc = stale.ToUniversalTime();

        if (observedAtUtc is { } normalizedObserved &&
            staleAfterUtc is { } normalizedStale &&
            normalizedStale <= normalizedObserved)
        {
            throw new ArgumentException(
                "Model discovery metadata must become stale after it was observed.",
                nameof(staleAfterUtc));
        }

        ArgumentNullException.ThrowIfNull(extensionData);
        ExtensionData = NormalizeExtensionData(extensionData);

        ModelId = normalizedModelId;
        OwnedBy = ownedBy;
        CreatedAtUtc = createdAtUtc;
        Availability = availability;
        Health = health;
        DiscoveredCapabilities = new ReadOnlyCollection<CapabilityStateEntry>(capabilities);
        DefaultThinkingLevel = defaultThinkingLevel;
        Limits = limits;
        Pricing = pricing;
        ObservedAtUtc = observedAtUtc;
        StaleAfterUtc = staleAfterUtc;
    }

    public string ModelId { get; }

    public string? OwnedBy { get; }

    public DateTimeOffset? CreatedAtUtc { get; }

    public ProviderAvailabilityStatus Availability { get; }

    public ProviderHealthStatus Health { get; }

    public IReadOnlyList<CapabilityStateEntry> DiscoveredCapabilities { get; }

    public IReadOnlyList<string> InputModalities { get; } = Array.Empty<string>();

    public IReadOnlyList<string> OutputModalities { get; } = Array.Empty<string>();

    public IReadOnlyList<string> ThinkingOptions { get; } = Array.Empty<string>();

    public string? DefaultThinkingLevel { get; }

    public ProviderModelLimits? Limits { get; }

    public ProviderModelPricing? Pricing { get; }

    public IReadOnlyDictionary<string, JsonElement> ExtensionData { get; } =
        new ReadOnlyDictionary<string, JsonElement>(
            new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public DateTimeOffset? ObservedAtUtc { get; }

    public DateTimeOffset? StaleAfterUtc { get; }

    public bool IsStale(DateTimeOffset nowUtc)
    {
        if (StaleAfterUtc is not { } staleAfter)
            return false;

        return nowUtc.ToUniversalTime() >= staleAfter;
    }

    private static IReadOnlyList<string> NormalizeStrings(
        IReadOnlyList<string>? values,
        int maxCount,
        int maxLength,
        string parameterName)
    {
        if (values is null || values.Count == 0)
            return Array.Empty<string>();

        if (values.Count > maxCount)
        {
            throw new ArgumentException(
                $"{parameterName} cannot contain more than {maxCount} entries.",
                parameterName);
        }

        var result = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var raw in values)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var normalized = raw.Trim();
            if (normalized.Length > maxLength)
            {
                throw new ArgumentException(
                    $"{parameterName} entries cannot exceed {maxLength} characters.",
                    parameterName);
            }

            if (seen.Add(normalized))
                result.Add(normalized);
        }

        return new ReadOnlyCollection<string>(result);
    }

    private static IReadOnlyDictionary<string, JsonElement> NormalizeExtensionData(
        IReadOnlyDictionary<string, JsonElement> extensionData)
    {
        if (extensionData.Count == 0)
            return new ReadOnlyDictionary<string, JsonElement>(
                new Dictionary<string, JsonElement>(StringComparer.Ordinal));

        if (extensionData.Count > MaxExtensionPropertyCount)
        {
            throw new ArgumentException(
                $"Provider model extension data cannot exceed {MaxExtensionPropertyCount} entries.",
                nameof(extensionData));
        }

        var normalized = new Dictionary<string, JsonElement>(
            StringComparer.Ordinal);
        var totalBytes = 0;

        foreach (var pair in extensionData)
        {
            if (string.IsNullOrWhiteSpace(pair.Key))
                throw new ArgumentException(
                    "Provider model extension keys are required.",
                    nameof(extensionData));

            var key = pair.Key.Trim();
            if (key.Length > MaxExtensionKeyLength)
            {
                throw new ArgumentException(
                    $"Provider model extension keys cannot exceed {MaxExtensionKeyLength} characters.",
                    nameof(extensionData));
            }

            if (pair.Value.ValueKind == JsonValueKind.Undefined)
            {
                throw new ArgumentException(
                    $"Provider model extension '{key}' has an invalid JSON value.",
                    nameof(extensionData));
            }

            var raw = pair.Value.GetRawText();
            if (raw.Length > MaxExtensionPropertyBytes)
            {
                throw new ArgumentException(
                    $"Provider model extension '{key}' exceeds the bounded value size.",
                    nameof(extensionData));
            }

            totalBytes += raw.Length;
            if (totalBytes > MaxExtensionTotalBytes)
            {
                throw new ArgumentException(
                    "Provider model extension data exceeds the bounded aggregate size.",
                    nameof(extensionData));
            }

            if (!normalized.TryAdd(key, pair.Value.Clone()))
            {
                throw new ArgumentException(
                    $"Duplicate provider model extension '{key}' is not allowed.",
                    nameof(extensionData));
            }
        }

        return new ReadOnlyDictionary<string, JsonElement>(normalized);
    }
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

        if (modelEnumerationState != ProviderDiscoveryState.Supported &&
            models.Count != 0)
        {
            throw new ArgumentException(
                "Unsupported or unknown model enumeration cannot contain discovered models.",
                nameof(models));
        }

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
            !ProviderEndpointIdentity.Equals(
                discovery.Endpoint,
                target.Endpoint))
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
