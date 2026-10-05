using System.Net;
using Hive.Core;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderPricingNormalizationTests
{
    [Fact]
    public async Task OpenRouterScalarTokenPricing_GetsExplicitPerTokenQuantity()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "openrouter-model",
                      "pricing": {
                        "prompt": "0.00000035",
                        "completion": "0.00000070",
                        "image": "0",
                        "request": "0"
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        var input = pricing.Prices.Single(
            price => price.BillingUnit == "input_token");
        var output = pricing.Prices.Single(
            price => price.BillingUnit == "output_token");

        Assert.Equal(1m, input.UnitQuantity);
        Assert.Equal(1m, output.UnitQuantity);
        Assert.Equal("USD", input.Currency);
        Assert.Equal("USD", output.Currency);
        Assert.Equal(0.70m, pricing.TryGetComparableTokenPricePerMillion());
        Assert.False(pricing.HasZeroComparableInputOutputTokenPricing);
        Assert.False(pricing.ExplicitFreeEvidence);
    }

    [Fact]
    public async Task StandardScalarTokenPricing_RemainsNonComparableWithoutEstablishedQuantity()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "unknown-unit-model",
                      "pricing": {
                        "input": 0.35,
                        "output": 0.75
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Null(pricing.Prices.Single(
            price => price.BillingUnit == "input_token").UnitQuantity);
        Assert.Null(pricing.Prices.Single(
            price => price.BillingUnit == "output_token").UnitQuantity);
        Assert.Null(pricing.TryGetComparableTokenPricePerMillion());
        Assert.False(pricing.HasCompleteComparableInputOutputTokenPricing);
        Assert.False(pricing.ExplicitFreeEvidence);
    }

    [Fact]
    public async Task TopLevelPricingUnitQuantity_AppliesToTokenRates()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "top-level-unit-model",
                      "pricing": {
                        "unit": "per_1k_tokens",
                        "input": 0.35,
                        "output": 0.75
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.All(
            pricing.Prices,
            price => Assert.Equal(1_000m, price.UnitQuantity));
        Assert.Equal(750m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task ExplicitQuantityWithoutCurrency_RemainsNonComparableToUsd()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "missing-currency-model",
                      "pricing": {
                        "input": {
                          "price": 0.35,
                          "unit_quantity": 1000000
                        },
                        "output": {
                          "price": 0.75,
                          "unit_quantity": 1000000
                        }
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.All(
            pricing.Prices,
            price => Assert.Equal(1_000_000m, price.UnitQuantity));
        Assert.All(
            pricing.Prices,
            price => Assert.Null(price.Currency));
        Assert.Null(pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task StandardObjectPricing_WithBillingDimensionOnly_DoesNotInferQuantity()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "object-pricing-model",
                      "pricing": {
                        "input": {
                          "price": 0.35,
                          "unit": "input_token"
                        },
                        "output": {
                          "price": 0.75,
                          "unit": "output_token"
                        }
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Null(pricing.Prices.Single(
            price => price.BillingUnit == "input_token").UnitQuantity);
        Assert.Null(pricing.Prices.Single(
            price => price.BillingUnit == "output_token").UnitQuantity);
        Assert.Null(pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task PaidTokenPricing_WithZeroAncillaryCharges_IsNotFree()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "paid-model",
                      "pricing": {
                        "prompt": "0.000001",
                        "completion": "0.000002",
                        "image": "0",
                        "request": "0"
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.False(pricing.ExplicitFreeEvidence);
        Assert.False(pricing.HasZeroComparableInputOutputTokenPricing);
        Assert.Equal(2m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task MixedPaidAndZeroTokenPricing_IsNotFree()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "mixed-model",
                      "pricing": {
                        "prompt": "0",
                        "completion": "0.000002"
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.False(pricing.ExplicitFreeEvidence);
        Assert.False(pricing.HasZeroComparableInputOutputTokenPricing);
        Assert.Equal(2m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task ZeroComparableInputAndOutputPricing_IsFree()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "free-model",
                      "pricing": {
                        "prompt": "0",
                        "completion": "0",
                        "image": "0",
                        "request": "0"
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://openrouter.ai/api/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://openrouter.ai/api/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.True(pricing.ExplicitFreeEvidence);
        Assert.True(pricing.HasZeroComparableInputOutputTokenPricing);
        Assert.Equal(0m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task ExplicitPricingUnit_DeterminesQuantity()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "explicit-unit-model",
                      "pricing": {
                        "input": {
                          "price": 0.35,
                          "unit": "per_1m_tokens"
                        },
                        "output": {
                          "price": 1.50,
                          "unit": "per_1m_tokens"
                        }
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Equal(
            1_000_000m,
            pricing.Prices.Single(
                price => price.BillingUnit == "input_token").UnitQuantity);
        Assert.Equal(
            1_000_000m,
            pricing.Prices.Single(
                price => price.BillingUnit == "output_token").UnitQuantity);
        Assert.Equal(1.50m, pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task DeclaredDefaultPricingVariant_DrivesComparablePrice()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "variant-model",
                      "pricing": {
                        "variants": {
                          "standard": {
                            "default": true,
                            "conditions": {
                              "service_tier": "standard"
                            },
                            "input": 0.00000035,
                            "output": 0.00000070
                          },
                          "batch": {
                            "conditions": {
                              "service_tier": "batch"
                            },
                            "input": 0.00000018,
                            "output": 0.00000035
                          }
                        }
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://example.test/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Equal(2, pricing.Variants.Count);
        Assert.Equal(
            "standard",
            Assert.Single(pricing.Variants, variant => variant.IsDefault).Key);
        Assert.Equal(0.70m, pricing.TryGetComparableTokenPricePerMillion());
        Assert.Equal(
            "standard",
            pricing.Variants
                .Single(variant => variant.IsDefault)
                .Conditions["service_tier"]);
    }

    [Fact]
    public async Task AmbiguousPricingVariants_RemainNonComparable()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "ambiguous-variant-model",
                      "pricing": {
                        "variants": {
                          "batch": {
                            "input": 0.00000018,
                            "output": 0.00000035
                          },
                          "priority": {
                            "input": 0.00000035,
                            "output": 0.00000070
                          }
                        }
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://example.test/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Equal(2, pricing.Variants.Count);
        Assert.Empty(pricing.Prices);
        Assert.Null(pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task MultipleDefaultPricingVariants_RemainAmbiguous()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "multiple-defaults",
                      "pricing": {
                        "variants": {
                          "standard": {
                            "default": true,
                            "input": 0.00000035,
                            "output": 0.00000070
                          },
                          "priority": {
                            "default": true,
                            "input": 0.00000070,
                            "output": 0.00000140
                          }
                        }
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://example.test/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.Equal(2, pricing.Variants.Count);
        Assert.Empty(pricing.Prices);
        Assert.All(
            pricing.Variants,
            variant => Assert.False(variant.IsDefault));
        Assert.Null(pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public async Task InvalidTopLevelUnitQuantity_DoesNotFallBackToFormatDefault()
    {
        using var client = new HttpClient(
            new FixedResponseHandler(
                """
                {
                  "data": [
                    {
                      "id": "invalid-unit-quantity",
                      "pricing": {
                        "unit_quantity": 0,
                        "input": 0.00000035,
                        "output": 0.00000070
                      }
                    }
                  ]
                }
                """));

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync(
            new Uri("https://example.test/v1/models"),
            OpenAICompatibleModelCatalogFormat.OpenRouter);

        Assert.True(result.IsSuccess, result.Error?.Message);

        var pricing = Assert.Single(result.Value!.Models).Pricing!;

        Assert.All(
            pricing.Prices,
            price => Assert.Null(price.UnitQuantity));
        Assert.Null(pricing.TryGetComparableTokenPricePerMillion());
    }

    [Fact]
    public void PricingVariant_EnforcesBoundedConditionsAndExplicitDefault()
    {
        var defaultVariant = new ProviderModelPricingVariant(
            "standard",
            [
                new ProviderModelPrice(
                    "input_token",
                    0.35m,
                    "USD",
                    1_000_000m),
                new ProviderModelPrice(
                    "output_token",
                    1.50m,
                    "USD",
                    1_000_000m)
            ],
            new Dictionary<string, string>
            {
                ["service_tier"] = "standard",
                ["region"] = "us"
            },
            isDefault: true);

        var batchVariant = new ProviderModelPricingVariant(
            "batch",
            [
                new ProviderModelPrice(
                    "input_token",
                    0.175m,
                    "USD",
                    1_000_000m),
                new ProviderModelPrice(
                    "output_token",
                    0.75m,
                    "USD",
                    1_000_000m)
            ],
            new Dictionary<string, string>
            {
                ["service_tier"] = "batch"
            });

        var pricing = new ProviderModelPricing(
            defaultVariant.Prices,
            variants: [defaultVariant, batchVariant]);

        Assert.Equal(2, pricing.Variants.Count);
        Assert.Single(pricing.Variants, variant => variant.IsDefault);
        Assert.Equal(1.50m, pricing.TryGetComparableTokenPricePerMillion());
    }

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FixedResponseHandler(string body)
        {
            _response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    body,
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_response);
    }
}
