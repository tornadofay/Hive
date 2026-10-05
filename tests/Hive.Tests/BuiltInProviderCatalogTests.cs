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

        var deepSeek = BuiltInProviderCatalog.Find("  DEEPSEEK  ");

        Assert.NotNull(deepSeek);
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
            var definition = BuiltInProviderCatalog.Find(key);

            Assert.NotNull(definition);
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
            var definition = BuiltInProviderCatalog.Find(key);

            Assert.NotNull(definition);
            Assert.False(definition!.NormalOnboardingSupported);
            Assert.Null(definition.DefaultEndpoint);
            Assert.Equal(
                BuiltInProviderDiscoveryEndpointKind.AccountOrRegionSpecific,
                definition.DiscoveryEndpointKind);
            Assert.False(string.IsNullOrWhiteSpace(definition.OnboardingNote));
        }
    }

    [Fact]
    public void ProviderSpecificDiscoveryProvidersRemainAdvancedOnly()
    {
        var ai21 = BuiltInProviderCatalog.Find("ai21");
        var minimax = BuiltInProviderCatalog.Find("minimax");

        Assert.NotNull(ai21);
        Assert.NotNull(minimax);

        foreach (var definition in new[] { ai21!, minimax! })
        {
            Assert.True(definition.IsOpenAICompatible);
            Assert.False(definition.RequiresNativeIntegration);
            Assert.False(definition.NormalOnboardingSupported);
            Assert.Null(definition.DefaultEndpoint);
            Assert.Equal(
                BuiltInProviderDiscoveryEndpointKind.ProviderSpecific,
                definition.DiscoveryEndpointKind);
            Assert.Equal(
                BuiltInProviderCredentialRequirement.Required,
                definition.CredentialRequirement);
            Assert.Equal(
                BuiltInProviderPricingNormalizationProfile.ProviderSpecific,
                definition.PricingNormalizationProfile);
            Assert.False(string.IsNullOrWhiteSpace(definition.OnboardingNote));
        }
    }

    [Fact]
    public void ExistingSpecialDiscoveryAndPricingProfilesRemainExplicit()
    {
        var openRouter = BuiltInProviderCatalog.Find("openrouter");
        var cerebras = BuiltInProviderCatalog.Find("cerebras");
        var gemini = BuiltInProviderCatalog.Find("google-gemini");
        var ollama = BuiltInProviderCatalog.Find("ollama");
        var cloudflare = BuiltInProviderCatalog.Find("cloudflare");

        Assert.NotNull(openRouter);
        Assert.NotNull(cerebras);
        Assert.NotNull(gemini);
        Assert.NotNull(ollama);
        Assert.NotNull(cloudflare);

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
