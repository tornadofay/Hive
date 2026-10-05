namespace Hive.Core;

public enum BuiltInProviderCredentialKind
{
    None,
    ApiKey,
    OptionalApiKey,
    ProviderSpecific
}

public enum BuiltInProviderCredentialRequirement
{
    None,
    Optional,
    Required
}

public enum BuiltInProviderIntegrationKind
{
    OpenAICompatible,
    NativeIntegrationRequired
}

public enum BuiltInProviderDiscoveryProfile
{
    StandardOpenAICompatible,
    OpenRouter,
    CerebrasOpenRouter,
    Gemini,
    Ollama,
    LmStudio,
    CloudflareOpenRouter,
    NativeIntegrationRequired
}

public enum BuiltInProviderDiscoveryEndpointKind
{
    DefaultEndpointModels,
    ProviderSpecific,
    AccountOrRegionSpecific,
    NativeIntegrationRequired
}

public enum BuiltInProviderPricingNormalizationProfile
{
    StandardOpenAICompatible,
    OpenRouterPerToken,
    CerebrasOpenRouter,
    CloudflareOpenRouter,
    ProviderSpecific,
    None
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
        string? onboardingNote = null,
        BuiltInProviderIntegrationKind integrationKind =
            BuiltInProviderIntegrationKind.OpenAICompatible,
        BuiltInProviderDiscoveryProfile discoveryProfile =
            BuiltInProviderDiscoveryProfile.StandardOpenAICompatible,
        BuiltInProviderDiscoveryEndpointKind discoveryEndpointKind =
            BuiltInProviderDiscoveryEndpointKind.DefaultEndpointModels,
        BuiltInProviderPricingNormalizationProfile pricingNormalizationProfile =
            BuiltInProviderPricingNormalizationProfile.StandardOpenAICompatible)
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

        if (!Enum.IsDefined(integrationKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(integrationKind),
                integrationKind,
                "Built-in provider integration kind is invalid.");
        }

        if (!Enum.IsDefined(discoveryProfile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(discoveryProfile),
                discoveryProfile,
                "Built-in provider discovery profile is invalid.");
        }

        if (!Enum.IsDefined(discoveryEndpointKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(discoveryEndpointKind),
                discoveryEndpointKind,
                "Built-in provider discovery endpoint kind is invalid.");
        }

        if (!Enum.IsDefined(pricingNormalizationProfile))
        {
            throw new ArgumentOutOfRangeException(
                nameof(pricingNormalizationProfile),
                pricingNormalizationProfile,
                "Built-in provider pricing normalization profile is invalid.");
        }

        if (normalOnboardingSupported && defaultEndpoint is null)
            throw new ArgumentException(
                "A normal-onboarding provider requires a default endpoint.",
                nameof(defaultEndpoint));

        if (integrationKind == BuiltInProviderIntegrationKind.OpenAICompatible &&
            !string.Equals(
                transportKind.Trim(),
                "openai-compatible",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "OpenAI-compatible built-in providers must use the openai-compatible transport kind.",
                nameof(transportKind));
        }

        if (integrationKind == BuiltInProviderIntegrationKind.NativeIntegrationRequired &&
            string.Equals(
                transportKind.Trim(),
                "openai-compatible",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Native-integration providers cannot use the openai-compatible transport kind.",
                nameof(transportKind));
        }

        if (discoveryProfile == BuiltInProviderDiscoveryProfile.NativeIntegrationRequired &&
            discoveryEndpointKind != BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired)
        {
            throw new ArgumentException(
                "Native discovery requires a native-integration discovery endpoint classification.",
                nameof(discoveryEndpointKind));
        }

        if (discoveryEndpointKind == BuiltInProviderDiscoveryEndpointKind.DefaultEndpointModels &&
            defaultEndpoint is null)
        {
            throw new ArgumentException(
                "A default model discovery endpoint classification requires a default endpoint.",
                nameof(defaultEndpoint));
        }

        if (onboardingNote is not null &&
            onboardingNote.Trim().Length > 512)
        {
            throw new ArgumentException(
                "Provider onboarding notes cannot exceed 512 characters.",
                nameof(onboardingNote));
        }

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
        IntegrationKind = integrationKind;
        DiscoveryProfile = discoveryProfile;
        DiscoveryEndpointKind = discoveryEndpointKind;
        PricingNormalizationProfile = pricingNormalizationProfile;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public string TransportKind { get; }

    public BuiltInProviderCredentialKind CredentialKind { get; }

    public Uri? DefaultEndpoint { get; }

    public bool NormalOnboardingSupported { get; }

    public string? OnboardingNote { get; }

    public BuiltInProviderIntegrationKind IntegrationKind { get; }

    public BuiltInProviderDiscoveryProfile DiscoveryProfile { get; }

    public BuiltInProviderDiscoveryEndpointKind DiscoveryEndpointKind { get; }

    public BuiltInProviderPricingNormalizationProfile PricingNormalizationProfile { get; }

    public bool IsOpenAICompatible =>
        IntegrationKind == BuiltInProviderIntegrationKind.OpenAICompatible;

    public bool RequiresNativeIntegration =>
        IntegrationKind == BuiltInProviderIntegrationKind.NativeIntegrationRequired;

    public BuiltInProviderCredentialRequirement CredentialRequirement =>
        CredentialKind switch
        {
            BuiltInProviderCredentialKind.None =>
                BuiltInProviderCredentialRequirement.None,

            BuiltInProviderCredentialKind.OptionalApiKey =>
                BuiltInProviderCredentialRequirement.Optional,

            BuiltInProviderCredentialKind.ApiKey or
            BuiltInProviderCredentialKind.ProviderSpecific =>
                BuiltInProviderCredentialRequirement.Required,

            _ => throw new InvalidOperationException(
                "Built-in provider credential kind is invalid.")
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
            new Uri("https://openrouter.ai/api/v1"),
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.OpenRouterPerToken),

        new(
            "cerebras",
            "Cerebras",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.cerebras.ai/v1"),
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.CerebrasOpenRouter,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.CerebrasOpenRouter),

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
            new Uri("https://generativelanguage.googleapis.com/v1beta/openai"),
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.Gemini,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.ProviderSpecific,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "ollama",
            "Ollama",
            "openai-compatible",
            BuiltInProviderCredentialKind.OptionalApiKey,
            new Uri("http://localhost:11434/v1"),
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.None),

        new(
            "lm-studio",
            "LM Studio",
            "openai-compatible",
            BuiltInProviderCredentialKind.OptionalApiKey,
            new Uri("http://localhost:1234/v1"),
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.LmStudio,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.None),

        new(
            "cloudflare",
            "Cloudflare",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Requires an account-specific Workers AI endpoint. Configure it through Advanced Provider Configuration.",
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.CloudflareOpenRouter,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.CloudflareOpenRouter),

        new(
            "deepseek",
            "DeepSeek",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.deepseek.com")),

        new(
            "qwen",
            "Qwen / Alibaba Cloud Model Studio",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "The OpenAI-compatible endpoint is workspace- and region-specific. Configure the Model Studio endpoint through Advanced Provider Configuration.",
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific),

        new(
            "moonshot",
            "Kimi / Moonshot AI",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.moonshot.ai/v1")),

        new(
            "xai",
            "xAI",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.x.ai/v1")),

        new(
            "mistral",
            "Mistral AI",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.mistral.ai/v1")),

        new(
            "cohere",
            "Cohere",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.cohere.ai/compatibility/v1")),

        new(
            "fireworks-ai",
            "Fireworks AI",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.fireworks.ai/inference/v1")),

        new(
            "together-ai",
            "Together AI",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.together.xyz/v1")),

        new(
            "perplexity",
            "Perplexity",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.perplexity.ai")),

        new(
            "minimax",
            "MiniMax",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.minimax.io/v1")),

        new(
            "ai21",
            "AI21 Labs",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.ai21.com/studio/v1")),

        new(
            "sambanova",
            "SambaNova",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.sambanova.ai/v1")),

        new(
            "deepinfra",
            "DeepInfra",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.deepinfra.com/v1/openai")),

        new(
            "nebius",
            "Nebius AI Studio",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.tokenfactory.nebius.cloud/v1")),

        new(
            "siliconflow",
            "SiliconFlow",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.siliconflow.cn/v1")),

        new(
            "zai",
            "Z.ai / GLM",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.z.ai/api/paas/v4")),

        new(
            "stepfun",
            "StepFun",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.stepfun.com/v1")),

        new(
            "baidu-qianfan",
            "Baidu Qianfan / ERNIE",
            "openai-compatible",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Authentication and endpoint configuration are provider-specific. Configure Qianfan through Advanced Provider Configuration.",
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "tencent-hunyuan",
            "Tencent Hunyuan",
            "openai-compatible",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Endpoint and authentication can depend on Tencent Cloud account/region configuration. Use Advanced Provider Configuration.",
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "volcengine",
            "ByteDance Volcengine / Doubao",
            "openai-compatible",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Ark endpoint configuration is region/account-specific. Configure the endpoint through Advanced Provider Configuration.",
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "writer",
            "Writer",
            "openai-compatible",
            BuiltInProviderCredentialKind.ApiKey,
            new Uri("https://api.writer.com/v1")),

        new(
            "anthropic",
            "Anthropic",
            "anthropic",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Requires the native Anthropic provider integration. It is represented in the catalog but is not routed through the OpenAI-compatible adapter.",
            integrationKind:
                BuiltInProviderIntegrationKind.NativeIntegrationRequired,
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "aws-bedrock",
            "AWS Bedrock",
            "aws-bedrock",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Requires the native AWS Bedrock integration and AWS account/region credentials. Use Advanced Provider Configuration.",
            integrationKind:
                BuiltInProviderIntegrationKind.NativeIntegrationRequired,
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "azure-openai",
            "Azure OpenAI",
            "azure-openai",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Requires the native Azure OpenAI integration and deployment/resource configuration. Use Advanced Provider Configuration.",
            integrationKind:
                BuiltInProviderIntegrationKind.NativeIntegrationRequired,
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "google-vertex-ai",
            "Google Vertex AI",
            "google-vertex-ai",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Requires the native Google Vertex AI integration and project/region credentials. Use Advanced Provider Configuration.",
            integrationKind:
                BuiltInProviderIntegrationKind.NativeIntegrationRequired,
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific),

        new(
            "replicate",
            "Replicate",
            "replicate",
            BuiltInProviderCredentialKind.ProviderSpecific,
            null,
            normalOnboardingSupported: false,
            onboardingNote:
                "Requires the native Replicate integration. It is cataloged here but is not routed through the OpenAI-compatible adapter.",
            integrationKind:
                BuiltInProviderIntegrationKind.NativeIntegrationRequired,
            discoveryProfile:
                BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
            discoveryEndpointKind:
                BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
            pricingNormalizationProfile:
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific)
    ];

    public static IReadOnlyList<BuiltInProviderDefinition> All => Definitions;

    public static BuiltInProviderDefinition? Find(string key) =>
        Definitions.FirstOrDefault(
            definition => string.Equals(
                definition.Key,
                key?.Trim(),
                StringComparison.OrdinalIgnoreCase));
}
