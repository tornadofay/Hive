using System.Collections.ObjectModel;

namespace Hive.Core;

public enum CapabilityState
{
    Supported,
    Unsupported,
    Unknown
}

public readonly record struct CapabilityKey
{
    public CapabilityKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Capability key is required.", nameof(value));

        var normalized = value.Trim();

        if (normalized.Length > 128)
            throw new ArgumentException(
                "Capability key cannot exceed 128 characters.",
                nameof(value));

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public sealed record CapabilityStateEntry
{
    public CapabilityStateEntry(
        CapabilityKey capability,
        CapabilityState state)
    {
        if (string.IsNullOrWhiteSpace(capability.Value))
            throw new ArgumentException("Capability key is required.", nameof(capability));

        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Capability state is invalid.");

        Capability = capability;
        State = state;
    }

    public CapabilityKey Capability { get; }

    public CapabilityState State { get; }
}

public sealed record Provider
{
    public Provider(
        ResourceEnvelope<ProviderId> resource,
        string key,
        string displayName,
        string transportKind)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Kind != ResourceKind.Provider)
        {
            throw new ArgumentException(
                "Provider resources must use ResourceKind.Provider.",
                nameof(resource));
        }

        Key = RequireText(key, nameof(key), 100);
        DisplayName = RequireText(displayName, nameof(displayName), 200);
        TransportKind = RequireText(transportKind, nameof(transportKind), 100);
        Resource = resource;
    }

    public ResourceEnvelope<ProviderId> Resource { get; }

    public ProviderId Id => Resource.Identity;

    public string Key { get; }

    public string DisplayName { get; }

    public string TransportKind { get; }

    public Provider WithDisplayName(string displayName) =>
        new(Resource, Key, displayName, TransportKind);

    public Provider WithTransportKind(string transportKind) =>
        new(Resource, Key, DisplayName, transportKind);

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException(
                $"{name} cannot exceed {maxLength} characters.",
                name);

        return normalized;
    }
}

public sealed record ProviderAccount
{
    public ProviderAccount(
        ResourceEnvelope<ProviderAccountId> resource,
        ProviderId providerId,
        string key,
        string displayName,
        string? externalAccountId = null,
        SecretReference? credentialSecret = null)
    {
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.Kind != ResourceKind.ProviderAccount)
        {
            throw new ArgumentException(
                "Provider account resources must use ResourceKind.ProviderAccount.",
                nameof(resource));
        }

        if (providerId == default)
            throw new ArgumentException(
                "Provider identity is required.",
                nameof(providerId));

        Key = RequireText(key, nameof(key), 100);
        DisplayName = RequireText(displayName, nameof(displayName), 200);
        ExternalAccountId = string.IsNullOrWhiteSpace(externalAccountId)
            ? null
            : RequireText(externalAccountId, nameof(externalAccountId), 200);
        CredentialSecret = credentialSecret;

        Resource = resource;
        ProviderId = providerId;
    }

    public ResourceEnvelope<ProviderAccountId> Resource { get; }

    public ProviderAccountId Id => Resource.Identity;

    public ProviderId ProviderId { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public string? ExternalAccountId { get; }

    public SecretReference? CredentialSecret { get; }

    public ProviderAccount WithDisplayName(string displayName) =>
        new(
            Resource,
            ProviderId,
            Key,
            displayName,
            ExternalAccountId,
            CredentialSecret);

    public ProviderAccount WithExternalAccountId(string? externalAccountId) =>
        new(
            Resource,
            ProviderId,
            Key,
            DisplayName,
            externalAccountId,
            CredentialSecret);

    public ProviderAccount WithCredentialSecret(SecretReference? credentialSecret) =>
        new(
            Resource,
            ProviderId,
            Key,
            DisplayName,
            ExternalAccountId,
            credentialSecret);

    private static string RequireText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
            throw new ArgumentException(
                $"{name} cannot exceed {maxLength} characters.",
                name);

        return normalized;
    }
}

public sealed record ExecutionTarget
{
    public ExecutionTarget(
        ResourceEnvelope<ExecutionTargetId> resource,
        ProviderId providerId,
        ProviderAccountId providerAccountId,
        string key,
        string displayName,
        Uri endpoint,
        string? model,
        string? deployment,
        IReadOnlyList<CapabilityStateEntry> capabilities)
    {
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(capabilities);

        if (resource.Kind != ResourceKind.ExecutionTarget)
        {
            throw new ArgumentException(
                "Execution target resources must use ResourceKind.ExecutionTarget.",
                nameof(resource));
        }

        if (providerId == default)
            throw new ArgumentException(
                "Provider identity is required.",
                nameof(providerId));

        if (providerAccountId == default)
        {
            throw new ArgumentException(
                "Provider account identity is required.",
                nameof(providerAccountId));
        }

        if (!endpoint.IsAbsoluteUri ||
            (endpoint.Scheme != Uri.UriSchemeHttp &&
             endpoint.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Execution target endpoint must be an absolute HTTP or HTTPS URI.",
                nameof(endpoint));
        }

        if (!string.IsNullOrEmpty(endpoint.UserInfo))
        {
            throw new ArgumentException(
                "Execution target endpoints must not embed credentials.",
                nameof(endpoint));
        }

        Model = NormalizeOptional(model, nameof(model), 512);
        Deployment = NormalizeOptional(deployment, nameof(deployment), 512);

        if (Model is null && Deployment is null)
        {
            throw new ArgumentException(
                "Either model or deployment is required.",
                nameof(model));
        }

        Key = RequireText(key, nameof(key), 100);
        DisplayName = RequireText(displayName, nameof(displayName), 200);
        Endpoint = endpoint;
        Capabilities = NormalizeCapabilities(capabilities);
        Resource = resource;
        ProviderId = providerId;
        ProviderAccountId = providerAccountId;
    }

    public ResourceEnvelope<ExecutionTargetId> Resource { get; }

    public ExecutionTargetId Id => Resource.Identity;

    public ProviderId ProviderId { get; }

    public ProviderAccountId ProviderAccountId { get; }

    public string Key { get; }

    public string DisplayName { get; }

    public Uri Endpoint { get; }

    public string? Model { get; }

    public string? Deployment { get; }

    public IReadOnlyList<CapabilityStateEntry> Capabilities { get; }

    public ExecutionTarget WithDisplayName(string displayName) =>
        new(
            Resource,
            ProviderId,
            ProviderAccountId,
            Key,
            displayName,
            Endpoint,
            Model,
            Deployment,
            Capabilities);

    public ExecutionTarget WithEndpoint(Uri endpoint) =>
        new(
            Resource,
            ProviderId,
            ProviderAccountId,
            Key,
            DisplayName,
            endpoint,
            Model,
            Deployment,
            Capabilities);

    public ExecutionTarget WithModel(string? model) =>
        new(
            Resource,
            ProviderId,
            ProviderAccountId,
            Key,
            DisplayName,
            Endpoint,
            model,
            Deployment,
            Capabilities);

    public ExecutionTarget WithDeployment(string? deployment) =>
        new(
            Resource,
            ProviderId,
            ProviderAccountId,
            Key,
            DisplayName,
            Endpoint,
            Model,
            deployment,
            Capabilities);

    public ExecutionTarget WithCapabilities(
        IReadOnlyList<CapabilityStateEntry> capabilities) =>
        new(
            Resource,
            ProviderId,
            ProviderAccountId,
            Key,
            DisplayName,
            Endpoint,
            Model,
            Deployment,
            capabilities);

    private static string RequireText(
        string value,
        string name,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{name} is required.", name);

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{name} cannot exceed {maxLength} characters.",
                name);
        }

        return normalized;
    }

    private static string? NormalizeOptional(
        string? value,
        string name,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();

        if (normalized.Length > maxLength)
        {
            throw new ArgumentException(
                $"{name} cannot exceed {maxLength} characters.",
                name);
        }

        return normalized;
    }

    private static IReadOnlyList<CapabilityStateEntry> NormalizeCapabilities(
        IReadOnlyList<CapabilityStateEntry> capabilities)
    {
        var result = new List<CapabilityStateEntry>(capabilities.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var capability in capabilities)
        {
            ArgumentNullException.ThrowIfNull(capability);

            if (!keys.Add(capability.Capability.Value))
            {
                throw new ArgumentException(
                    $"Duplicate capability '{capability.Capability.Value}' is not allowed.",
                    nameof(capabilities));
            }

            result.Add(capability);
        }

        return new ReadOnlyCollection<CapabilityStateEntry>(result);
    }
}
