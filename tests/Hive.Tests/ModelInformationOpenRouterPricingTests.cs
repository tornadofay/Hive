using System.Globalization;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

/// <summary>
/// Reproduces the reported OpenRouter "every model disappears" defect.
/// </summary>
public sealed class ModelInformationOpenRouterPricingTests
{
    private const string OpenRouterCatalogJson = """
        {
          "data": [
            {
              "id": "openrouter/cheap",
              "pricing": {
                "prompt": "0.0000007",
                "completion": "0.00000014"
              }
            }
          ]
        }
        """;

    private const string TieredPricingCatalogJson = """
        {
          "data": [
            {
              "id": "openai/gpt-6-sol",
              "pricing": {
                "prompt": "0.000002",
                "completion": "0.00001",
                "input_cache_read": "0.0000002",
                "overrides": [
                  {
                    "min_prompt_tokens": 272000,
                    "prompt": "0.000004",
                    "completion": "0.000015"
                  }
                ]
              }
            }
          ]
        }
        """;

    private const string ReasoningCatalogJson = """
        {
          "data": [
            {
              "id": "openai/reasoner",
              "reasoning": {
                "mandatory": false,
                "default_enabled": true,
                "supported_efforts": ["max", "high", "medium", "low", "none"],
                "default_effort": "medium"
              }
            }
          ]
        }
        """;

    [Fact]
    public async Task OpenRouterPricingOverrides_BecomePreservedPricingVariants()
    {
        // Live OpenRouter shape: tiered pricing is an ARRAY of threshold
        // overrides, not the object-shaped "variants" form. It was silently
        // discarded, so a user only ever saw the base rate.
        using var client = new HttpClient(
            new FixedResponseHandler(TieredPricingCatalogJson));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        var tier = Assert.Single(pricing.Variants);

        Assert.Contains("272000", tier.Key, StringComparison.Ordinal);
        Assert.Equal("272000", tier.Conditions["min_prompt_tokens"]);

        var tierInput = tier.Prices.Single(
            price => price.BillingUnit == "input_token");

        // 0.000004 per token = $4.00 per 1M tokens.
        Assert.Equal(
            4m,
            tierInput.Price / tierInput.UnitQuantity!.Value * 1_000_000m);

        // The base rate remains the comparable headline price.
        Assert.Equal(10m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task OpenRouterReasoningObject_ExposesEffortLevelsAndDefault()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(ReasoningCatalogJson));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var model = Assert.Single(result.Value!.Models);

        Assert.Equal(
            ["max", "high", "medium", "low", "none"],
            model.ThinkingOptions);

        Assert.Equal("medium", model.DefaultThinkingLevel);
    }

    [Fact]
    public async Task StandardFormat_LeavesOpenRouterRatesNonComparable()
    {
        // A provider configured as plain OpenAI-compatible (Standard format)
        // has no established currency or source quantity.
        using var client = new HttpClient(
            new FixedResponseHandler(OpenRouterCatalogJson));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.True(
            pricing.TryGetComparableTokenPricePerMillion() is null,
            "Standard format must not invent a comparable rate.");

        Assert.Null(
            pricing.Prices.Single(
                price => price.BillingUnit == "input_token").UnitQuantity);
    }

    [Fact]
    public async Task OpenRouterFormat_MakesTheSameRatesComparable()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(OpenRouterCatalogJson));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Equal(0.70m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public void UnknownPricingModels_AreHiddenByABoundedRangeUnlessIncluded()
    {
        // This is the reported symptom: a model with no comparable pricing rate.
        var unknown = CreateModel(
            "openrouter/cheap",
            [
                // No currency and no source quantity: not comparable.
                new ProviderModelPrice("input_token", 0.0000007m),
                new ProviderModelPrice("output_token", 0.00000014m)
            ]);

        Assert.Null(ModelInformationFilter.GetComparableTokenPricePerMillion(unknown));

        // Full range: visible, because nothing is constrained.
        Assert.True(ModelInformationFilter.Matches(
            unknown,
            new ModelFilterCriteria(0m, 5m, true, null,
                ModelCapabilityFilterState.Any)));

        // Bounded: excluded by established behaviour, and therefore the view
        // must say so rather than presenting an empty catalog.
        Assert.False(ModelInformationFilter.Matches(
            unknown,
            new ModelFilterCriteria(0m, 4.78m, false, null,
                ModelCapabilityFilterState.Any)));

        // Explicitly requested: visible again.
        Assert.True(ModelInformationFilter.Matches(
            unknown,
            new ModelFilterCriteria(0m, 4.78m, false, null,
                ModelCapabilityFilterState.Any,
                UnknownPricingVisibility.Include)));
    }

    [Theory]
    [InlineData("0.70", "1.00")]
    [InlineData("4.78", "5.00")]
    [InlineData("75", "100")]
    public void CalculatePriceCeiling_UsesTheDataNotAHighOutlier(
        string anchor,
        string expected)
    {
        // A dominant price with one extreme outlier.
        var prices = new List<decimal>();
        prices.AddRange(Enumerable.Repeat(
            decimal.Parse(anchor, CultureInfo.InvariantCulture),
            95));
        prices.Add(600m);

        Assert.Equal(
            decimal.Parse(expected, CultureInfo.InvariantCulture),
            ModelInformationFilter.CalculatePriceCeiling(prices));
    }

    [Fact]
    public void CalculatePriceCeiling_IgnoresAFewExtremeOutliers()
    {
        // Real OpenRouter-like shape: almost everything is cheap, a handful is
        // not. The ceiling must stay usable instead of jumping to the outlier.
        var prices = new List<decimal>();
        prices.AddRange(Enumerable.Repeat(0.70m, 470));
        prices.AddRange([120m, 300m, 600m]);

        var ceiling = ModelInformationFilter.CalculatePriceCeiling(prices);

        Assert.True(
            ceiling < 100m,
            $"Ceiling {ceiling} must not be dominated by a few outliers.");

        // The outliers stay counted so the view can report and expose them.
        Assert.True(
            ModelInformationFilter.CountAboveCeiling(prices, ceiling) > 0);
    }

    [Fact]
    public void CalculatePriceCeiling_WithoutComparablePricingIsSmallAndUsable()
    {
        Assert.Equal(1m, ModelInformationFilter.CalculatePriceCeiling([]));
        Assert.Equal(1m, ModelInformationFilter.CalculatePriceCeiling([0m, 0m]));
    }

    [Fact]
    public void BuiltInCatalog_OpenRouterUsesTheOpenRouterDiscoveryProfile()
    {
        // Regression guard. The built-in OpenRouter entry previously left
        // discoveryProfile at its StandardOpenAICompatible default, so OpenRouter
        // per-token rates arrived with no currency or source quantity, were not
        // comparable, and every model vanished as soon as the price slider was
        // bounded.
        var definition = BuiltInProviderCatalog.Find("openrouter");

        Assert.NotNull(definition);
        Assert.Equal(
            BuiltInProviderDiscoveryProfile.OpenRouter,
            definition.DiscoveryProfile);
        Assert.Equal(
            "openai-compatible",
            definition.TransportKind);
    }

    [Fact]
    public void BuiltInCatalog_EveryDefinitionDeclaresAnIntendedDiscoveryProfile()
    {
        // OpenRouter is the only provider that must not use the standard
        // discovery shape, so it must never fall back to the default again.
        var openRouter = BuiltInProviderCatalog.Find("openrouter");

        Assert.NotNull(openRouter);
        Assert.NotEqual(
            BuiltInProviderDiscoveryProfile.StandardOpenAICompatible,
            openRouter.DiscoveryProfile);
    }

    [Fact]
    public void PerTokenOpenRouterRates_AreComparableAndSurviveABoundedSelection()
    {
        var model = CreateModel(
            "openrouter/cheap",
            [
                new ProviderModelPrice("input_token", 0.0000007m, "USD", 1m),
                new ProviderModelPrice("output_token", 0.00000014m, "USD", 1m)
            ]);

        Assert.Equal(
            0.70m,
            ModelInformationFilter.GetComparableTokenPricePerMillion(model));

        var bounded = new ModelFilterCriteria(
            MinimumPricePerMillion: 0m,
            MaximumPricePerMillion: 4.78m,
            IsFullPriceRange: false,
            CapabilityKey: null,
            CapabilityState: ModelCapabilityFilterState.Any);

        Assert.True(ModelInformationFilter.Matches(model, bounded));
    }

    [Fact]
    public void UnknownPricingModels_AreNotSilentlyHiddenByABoundedPriceRange()
    {
        var unknown = CreateModel(
            "openrouter/cheap",
            [
                new ProviderModelPrice("input_token", 0.0000007m),
                new ProviderModelPrice("output_token", 0.00000014m)
            ]);

        // This is the reported regression: the model has no comparable rate, so
        // bounding the price slider previously removed it with no explanation.
        Assert.True(
            ModelInformationFilter.Matches(
                unknown,
                new ModelFilterCriteria(
                    0m,
                    4.78m,
                    IsFullPriceRange: false,
                    CapabilityKey: null,
                    CapabilityState: ModelCapabilityFilterState.Any,
                    UnknownPricing: UnknownPricingVisibility.Include)),
            "A model without comparable pricing must be restorable on request.");

        Assert.False(
            ModelInformationFilter.Matches(
                unknown,
                new ModelFilterCriteria(
                    0m,
                    4.78m,
                    IsFullPriceRange: false,
                    CapabilityKey: null,
                    CapabilityState: ModelCapabilityFilterState.Any,
                    UnknownPricing: UnknownPricingVisibility.Exclude)));
    }

    [Fact]
    public void CountWithoutComparablePricing_ExcludesFreeModels()
    {
        var unknown = CreateModel(
            "openrouter/unpriced",
            [new ProviderModelPrice("request", 0m)]);

        var free = CreateModel(
            "openrouter/free",
            [
                new ProviderModelPrice("input_token", 0m, "USD", 1m),
                new ProviderModelPrice("output_token", 0m, "USD", 1m)
            ]);

        var comparable = CreateModel(
            "openrouter/paid",
            [new ProviderModelPrice("input_token", 0.5m, "USD", 1m)]);

        // Free models are a known state, not a missing one.
        Assert.Equal(
            1,
            ModelInformationFilter.CountWithoutComparablePricing(
                [unknown, free, comparable]));
    }

    private static ProviderModelMetadata CreateModel(
        string modelId,
        IReadOnlyList<ProviderModelPrice> prices) =>
        new(
            modelId,
            "openrouter",
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [new CapabilityStateEntry(
                HiveCapabilityKeys.TextGeneration,
                CapabilityState.Supported)],
            ["text"],
            ["text"],
            pricing: new ProviderModelPricing(prices));

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly string _body;

        public FixedResponseHandler(string body) => _body = body;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(
                new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        _body,
                        System.Text.Encoding.UTF8,
                        "application/json")
                });
        }
    }
}