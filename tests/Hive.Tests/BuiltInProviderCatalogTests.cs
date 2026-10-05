using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class BuiltInProviderCatalogTests
{
    [Fact]
    public void CatalogContainsTheCompleteBoundedProviderInventory()
    {
        var expectedKeys = new[]
        {
            "openai",
            "groq",
            "openrouter",
            "cerebras",
            "nvidia",
            "google-gemini",
            "ollama",
            "lm-studio",
            "cloudflare",
            "deepseek",
            "qwen",
            "moonshot",
            "xai",
            "mistral",
            "cohere",
            "fireworks-ai",
            "together-ai",
            "perplexity",
            "minimax",
            "ai21",
            "sambanova",
            "deepinfra",
            "nebius",
            "siliconflow",
            "zai",
            "stepfun",
            "baidu-qianfan",
            "tencent-hunyuan",
            "volcengine",
            "writer",
            "anthropic",
            "aws-bedrock",
            "azure-openai",
            "google-vertex-ai",
            "replicate"
        };

        var actualKeys = BuiltInProviderCatalog.All
            .Select(static definition => definition.Key)
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            expectedKeys.OrderBy(static key => key, StringComparer.Ordinal),
            actualKeys);
        Assert.Equal(expectedKeys.Length, BuiltInProviderCatalog.All.Count);
    }

    [Fact]
    public void CatalogKeysAreUniqueAndFindIsCaseInsensitive()
    {
        var normalizedKeys = BuiltInProviderCatalog.All
            .Select(static definition => definition.Key)
            .Select(static key => key.Trim().ToUpperInvariant())
            .ToArray();

        Assert.Equal(
            normalizedKeys.Length,
            normalizedKeys.Distinct(StringComparer.Ordinal).Count());

        var deepSeek = Assert.NotNull(
            BuiltInProviderCatalog.Find("  DEEPSEEK  "));

        Assert.Equal("deepseek", deepSeek!.Key);
    }

    [Fact]
    public void OpenAiCompatibleEntriesUseTheSharedTransport()
    {
        Assert.All(
            BuiltInProviderCatalog.All
                .Where(static definition => definition.IsOpenAICompatible),
            definition =>
            {
                Assert.Equal(
                    "openai-compatible",
                    definition.TransportKind,
                    ignoreCase: true);
                Assert.False(definition.RequiresNativeIntegration);
                Assert.NotEqual(
                    BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
                    definition.DiscoveryProfile);
                Assert.NotEqual(
                    BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
                    definition.DiscoveryEndpointKind);
            });
    }

    [Fact]
    public void NativeProvidersAreCatalogedWithoutPretendingOpenAiCompatibility()
    {
        var nativeKeys = new[]
        {
            "anthropic",
            "aws-bedrock",
            "azure-openai",
            "google-vertex-ai",
            "replicate"
        };

        foreach (var key in nativeKeys)
        {
            var definition = Assert.NotNull(BuiltInProviderCatalog.Find(key));

            Assert.True(definition!.RequiresNativeIntegration);
            Assert.False(definition.IsOpenAICompatible);
            Assert.NotEqual(
                "openai-compatible",
                definition.TransportKind,
                ignoreCase: true);
            Assert.False(definition.NormalOnboardingSupported);
            Assert.Null(definition.DefaultEndpoint);
            Assert.Equal(
                BuiltInProviderCredentialRequirement.Required,
                definition.CredentialRequirement);
            Assert.Equal(
                BuiltInProviderCredentialKind.ProviderSpecific,
                definition.CredentialKind);
            Assert.Equal(
                BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
                definition.DiscoveryProfile);
            Assert.Equal(
                BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
                definition.DiscoveryEndpointKind);
            Assert.Equal(
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific,
                definition.PricingNormalizationProfile);
            Assert.False(string.IsNullOrWhiteSpace(definition.OnboardingNote));
        }
    }

    [Fact]
    public void NormalOnboardingProvidersHaveSafeDefaultEndpoints()
    {
        Assert.All(
            BuiltInProviderCatalog.All
                .Where(static definition => definition.NormalOnboardingSupported),
            definition =>
            {
                Assert.NotNull(definition.DefaultEndpoint);
                Assert.True(definition.DefaultEndpoint!.IsAbsoluteUri);
                Assert.Contains(
                    definition.DefaultEndpoint.Scheme,
                    new[] { Uri.UriSchemeHttp, Uri.UriSchemeHttps });
                Assert.Equal(string.Empty, definition.DefaultEndpoint.UserInfo);
                Assert.Equal(
                    BuiltInProviderDiscoveryEndpointKind.DefaultEndpointModels,
                    definition.DiscoveryEndpointKind);
            });
    }

    [Fact]
    public void AccountOrRegionSpecificProvidersDoNotInventUniversalEndpoints()
    {
        var keys = new[]
        {
            "cloudflare",
            "qwen",
            "baidu-qianfan",
            "tencent-hunyuan",
            "volcengine"
        };

        foreach (var key in keys)
        {
            var definition = Assert.NotNull(BuiltInProviderCatalog.Find(key));

            Assert.False(definition!.NormalOnboardingSupported);
            Assert.Null(definition.DefaultEndpoint);
            Assert.Equal(
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
                definition.DiscoveryEndpointKind);
            Assert.False(string.IsNullOrWhiteSpace(definition.OnboardingNote));
        }
    }

    [Fact]
    public void ExistingSpecialDiscoveryAndPricingProfilesRemainExplicit()
    {
        var openRouter = Assert.NotNull(BuiltInProviderCatalog.Find("openrouter"));
        var cerebras = Assert.NotNull(BuiltInProviderCatalog.Find("cerebras"));
        var gemini = Assert.NotNull(BuiltInProviderCatalog.Find("google-gemini"));
        var ollama = Assert.NotNull(BuiltInProviderCatalog.Find("ollama"));
        var cloudflare = Assert.NotNull(BuiltInProviderCatalog.Find("cloudflare"));

        Assert.Equal(
            BuiltInProviderPricingNormalizationProfile.OpenRouterPerToken,
            openRouter!.PricingNormalizationProfile);

        Assert.Equal(
            BuiltInProviderDiscoveryProfile.CerebrasOpenRouter,
            cerebras!.DiscoveryProfile);

        Assert.Equal(
            BuiltInProviderPricingNormalizationProfile.CerebrasOpenRouter,
            cerebras.PricingNormalizationProfile);

        Assert.Equal(
            BuiltInProviderDiscoveryProfile.Gemini,
            gemini!.DiscoveryProfile);

        Assert.Equal(
            BuiltInProviderPricingNormalizationProfile.ProviderSpecific,
            gemini.PricingNormalizationProfile);

        Assert.Equal(
            BuiltInProviderDiscoveryProfile.Ollama,
            ollama!.DiscoveryProfile);

        Assert.Equal(
            BuiltInProviderPricingNormalizationProfile.None,
            ollama.PricingNormalizationProfile);

        Assert.Equal(
            BuiltInProviderDiscoveryProfile.CloudflareOpenRouter,
            cloudflare!.DiscoveryProfile);

        Assert.Equal(
            BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
            cloudflare.DiscoveryEndpointKind);

        Assert.Equal(
            BuiltInProviderPricingNormalizationProfile.CloudflareOpenRouter,
            cloudflare.PricingNormalizationProfile);
    }

    [Fact]
    public void DefinitionRejectsNativeTransportMarkedAsOpenAiCompatible()
    {
        Assert.Throws<ArgumentException>(
            () => new BuiltInProviderDefinition(
                "invalid",
                "Invalid",
                "openai-compatible",
                BuiltInProviderCredentialKind.ApiKey,
                new Uri("https://example.test/v1"),
                integrationKind:
                    BuiltInProviderIntegrationKind.NativeIntegrationRequired,
                discoveryProfile:
                    BuiltInProviderDiscoveryProfile.NativeIntegrationRequired,
                discoveryEndpointKind:
                    BuiltInProviderDiscoveryEndpointKind.NativeIntegrationRequired,
                pricingNormalizationProfile:
                    BuiltInProviderPricingNormalizationProfile.ProviderSpecific));
    }
}
