namespace Hive.Core;

public enum BuiltInProviderCredentialKind
{
    None,
    ApiKey,
    OptionalApiKey
}

public enum BuiltInProviderCredentialRequirement
{
    None,
    Optional,
    Required
}

public sealed record BuiltInProviderDefinition
{
    public BuiltInProviderDefinition(
        string key,
        string displayName,
        string transportKind,
        BuiltInProviderCredentialKind credentialKind,
        Uri? defaultEndpoint,
        bool normalOnboardingSupported = true,
        string? onboardingNote = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Provider key is required.", nameof(key));

        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Provider display name is required.", nameof(displayName));

        if (string.IsNullOrWhiteSpace(transportKind))
            throw new ArgumentException("Provider transport kind is required.", nameof(transportKind));

        if (!Enum.IsDefined(credentialKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(credentialKind),
                credentialKind,
                "Built-in provider credential kind is invalid.");
        }

        if (normalOnboardingSupported && defaultEndpoint is null)
            throw new ArgumentException(
                "A normal-onboarding provider requires a default endpoint.",
                nameof(defaultEndpoint));

        if (defaultEndpoint is not null &&
            (!defaultEndpoint.IsAbsoluteUri ||
             (defaultEndpoint.Scheme != Uri.UriSchemeHttp &&
              defaultEndpoint.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(defaultEndpoint.UserInfo)))
        {
            throw new ArgumentException(
                "Default provider endpoint must be an absolute HTTP or HTTPS URI without embedded credentials.",
                nameof(defaultEndpoint));
        }

        Key = key.Trim();
        DisplayName = displayName.Trim();
        TransportKind = transportKind.Trim();
        CredentialKind = credentialKind;
        DefaultEndpoint = defaultEndpoint;
        NormalOnboardingSupported = normalOnboardingSupported;
        OnboardingNote = string.IsNullOrWhiteSpace(onboardingNote)
            ? null
            : onboardingNote.Trim();
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string TransportKind { get; }

    public BuiltInProviderCredentialKind CredentialKind { get; }

    public Uri? DefaultEndpoint { get; }

    public bool NormalOnboardingSupported { get; }

    public string? OnboardingNote { get; }

    public BuiltInProviderCredentialRequirement CredentialRequirement =>
        CredentialKind switch
        {
            BuiltInProviderCredentialKind.None => BuiltInProviderCredentialRequirement.None,
            BuiltInProviderCredentialKind.OptionalApiKey => BuiltInProviderCredentialRequirement.Optional,
            BuiltInProviderCredentialKind.ApiKey => BuiltInProviderCredentialRequirement.Required,
            _ => throw new InvalidOperationException("Built-in provider credential kind is invalid.")
        };

    public bool RequiresCredential =>
        CredentialRequirement == BuiltInProviderCredentialRequirement.Required;

    public bool AllowsOptionalCredential =>
        CredentialRequirement == BuiltInProviderCredentialRequirement.Optional;
}

public static class BuiltInProviderCatalog
{
    private static readonly IReadOnlyList<BuiltInProviderDefinition> Definitions =
    [
        new(
            "openai",
            "OpenAI",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.openai.com/v1")),
        new(
            "groq",
            "Groq",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.groq.com/openai/v1")),
        new(
            "openrouter",
            "OpenRouter",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://openrouter.ai/api/v1")),
        new(
            "cerebras",
            "Cerebras",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.cerebras.ai/v1")),
        new(
            "nvidia",
            "NVIDIA",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://integrate.api.nvidia.com/v1")),
        new(
            "google-gemini",
            "Google Gemini",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://generativelanguage.googleapis.com/v1beta/openai")),
        new(
            "ollama",
            "Ollama",
            "openai-compatible",
            BuiltInProviderCredentialKind.OptionalApiKey,
            new Uri("http://localhost:11434/v1")),
        new(
            "lm-studio",
            "LM Studio",
            "openai-compatible",
            BuiltInProviderCredentialKind.OptionalApiKey,
            new Uri("http://localhost:1234/v1")),
        new(
            "cloudflare",
            "Cloudflare",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            null,
            normalOnboardingSupported: false,
            onboardingNote: "Requires an account-specific endpoint. Configure it through Advanced Provider Configuration.")
    ];

    public static IReadOnlyList<BuiltInProviderDefinition> All => Definitions;

    public static BuiltInProviderDefinition? Find(string key) =>
        Definitions.FirstOrDefault(
            definition => string.Equals(
                definition.Key,
                key?.Trim(),
                StringComparison.OrdinalIgnoreCase));
}
