using System.Globalization;
using Hive.Core;
using Hive.Host.WinForms;
using Xunit;

namespace Hive.Tests;

/// <summary>
/// Direct coverage for the extracted Model Information filter rule.
/// </summary>
/// <remarks>
/// The filter rule previously lived only inside
/// <c>HiveModelInformationSettingsView.MatchesFilters</c>, entangled with live
/// slider/combo state, which is why a filter defect previously required a long
/// convergence loop. These tests exercise the rule directly, with no WinForms
/// control involved.
/// </remarks>
public sealed class ModelInformationFilterTests
{
    [Fact]
    public void FullRangeCriteria_IncludesModelsWithAndWithoutComparablePricing()
    {
        var priced = CreateModel(
            "priced-model",
            [TokenPrice("input_token", 1.25m), TokenPrice("output_token", 5m)]);
        var unknown = CreateModel("unknown-pricing-model", null);

        var criteria = FullRange();

        Assert.True(ModelInformationFilter.Matches(priced, criteria));
        Assert.True(ModelInformationFilter.Matches(unknown, criteria));
    }

    [Fact]
    public void BoundedPriceCriteria_ExcludesModelsWithoutComparablePricing()
    {
        var unknown = CreateModel("unknown-pricing-model", null);

        var criteria = new ModelFilterCriteria(
            MinimumPricePerMillion: 0m,
            MaximumPricePerMillion: 5m,
            IsFullPriceRange: false,
            CapabilityKey: null,
            CapabilityState: ModelCapabilityFilterState.Any);

        // Established behaviour: a bounded selection excludes models with no
        // comparable rate. The view must explain this rather than silently
        // emptying the catalog.
        Assert.False(ModelInformationFilter.Matches(unknown, criteria));

        Assert.True(ModelInformationFilter.Matches(
            unknown,
            criteria with { UnknownPricing = UnknownPricingVisibility.Include }));

        Assert.True(ModelInformationFilter.Matches(unknown, FullRange()));
    }

    [Fact]
    public void ZeroMaximum_ShowsOnlyFreeModels()
    {
        var free = CreateModel(
            "free-model",
            [TokenPrice("input_token", 0m), TokenPrice("output_token", 0m)]);
        var paid = CreateModel(
            "paid-model",
            [TokenPrice("input_token", 1.25m), TokenPrice("output_token", 5m)]);

        var freeOnly = FreeOnlyCriteria();

        Assert.True(ModelInformationFilter.Matches(free, freeOnly));
        Assert.False(ModelInformationFilter.Matches(paid, freeOnly));
    }

    [Fact]
    public void ZeroMaximum_IncludesExplicitFreeEvidenceEvenWithPaidComparableBaseRates()
    {
        // The regression that previously required eight commits: explicit free
        // evidence must win over paid input/output token base rates.
        var model = new ProviderModelMetadata(
            "explicit-free-with-paid-rates",
            "example-provider",
            Timestamp,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [],
            ["text"],
            ["text"],
            pricing: new ProviderModelPricing(
                [TokenPrice("input_token", 1.25m), TokenPrice("output_token", 5m)],
                explicitFreeEvidence: true));

        Assert.True(
            ModelInformationFilter.Matches(model, FreeOnlyCriteria()));
    }

    [Fact]
    public void ZeroMaximum_DoesNotTreatMissingPricingAsFree()
    {
        var unknown = CreateModel("unknown-pricing-model", null);

        Assert.False(
            ModelInformationFilter.Matches(unknown, FreeOnlyCriteria()));
    }

    [Fact]
    public void ExplicitFreeProfileWithoutTokenRates_HasComparableZero()
    {
        var model = new ProviderModelMetadata(
            "free-without-token-rates",
            "example-provider",
            Timestamp,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            [],
            ["text"],
            ["text"],
            pricing: new ProviderModelPricing(
                [TokenPrice("request", 0m)],
                explicitFreeEvidence: true));

        Assert.Equal(
            0m,
            ModelInformationFilter.GetComparableTokenPricePerMillion(model));
        Assert.True(ModelInformationFilter.IsFreeModel(model, 0m));
    }

    [Fact]
    public void ComparablePrice_NormalizesPerTokenRatesToMillions()
    {
        // $0.00000070 per token is $0.70 per million tokens.
        var model = CreateModel(
            "per-token-model",
            [
                PerTokenPrice("input_token", 0.00000035m),
                PerTokenPrice("output_token", 0.00000070m)
            ]);

        Assert.Equal(
            0.70m,
            ModelInformationFilter.GetComparableTokenPricePerMillion(model));
    }

    [Fact]
    public void ComparablePrice_IsTheHighestComparableTokenRate()
    {
        // The comparable price is the highest of the comparable input/output
        // token rates, not the output rate alone.
        var model = CreateModel(
            "priced-model",
            [
                TokenPrice("input_token", 0.25m),
                TokenPrice("output_token", 5m)
            ]);

        Assert.Equal(
            5m,
            ModelInformationFilter.GetComparableTokenPricePerMillion(model));
    }

    [Theory]
    [InlineData("0.70", "0.70", true)]
    [InlineData("0.70", "0.71", false)]
    [InlineData("0.70", "0.69", true)]
    [InlineData("0.69", "0.70", false)]
    public void ComparablePrice_RangeBoundariesAreInclusive(
        string maximum,
        string modelRate,
        bool expected)
    {
        // Decimal strings, not doubles: a double round-trip loses the precision
        // the filter compares on. Both token rates use the model rate so the
        // comparable price is exactly the value under test.
        var rate = decimal.Parse(modelRate, CultureInfo.InvariantCulture);

        var model = CreateModel(
            "priced-model",
            [TokenPrice("input_token", rate), TokenPrice("output_token", rate)]);

        var criteria = new ModelFilterCriteria(
            MinimumPricePerMillion: 0m,
            MaximumPricePerMillion: decimal.Parse(maximum, CultureInfo.InvariantCulture),
            IsFullPriceRange: false,
            CapabilityKey: null,
            CapabilityState: ModelCapabilityFilterState.Any);

        Assert.Equal(expected, ModelInformationFilter.Matches(model, criteria));
    }

    [Theory]
    [InlineData("1.00", "5.00", true)]
    [InlineData("5.00", "5.00", true)]
    [InlineData("5.01", "5.00", false)]
    public void ComparablePrice_MinimumBoundaryIsInclusive(
        string minimum,
        string modelRate,
        bool expected)
    {
        var rate = decimal.Parse(modelRate, CultureInfo.InvariantCulture);

        var model = CreateModel(
            "priced-model",
            [TokenPrice("input_token", rate), TokenPrice("output_token", rate)]);

        var criteria = new ModelFilterCriteria(
            MinimumPricePerMillion: decimal.Parse(minimum, CultureInfo.InvariantCulture),
            MaximumPricePerMillion: 10m,
            IsFullPriceRange: false,
            CapabilityKey: null,
            CapabilityState: ModelCapabilityFilterState.Any);

        Assert.Equal(expected, ModelInformationFilter.Matches(model, criteria));
    }

    [Fact]
    public void CapabilityFilter_MatchesSupportedStateOnly()
    {
        var model = CreateModel(
            "vision-model",
            null,
            [new CapabilityStateEntry(HiveCapabilityKeys.Vision, CapabilityState.Supported)]);

        Assert.True(ModelInformationFilter.Matches(
            model,
            Capability(HiveCapabilityKeys.Vision, ModelCapabilityFilterState.Supported)));

        Assert.False(ModelInformationFilter.Matches(
            model,
            Capability(HiveCapabilityKeys.Vision, ModelCapabilityFilterState.Unsupported)));
    }

    [Fact]
    public void CapabilityFilter_UnknownStateMatchesUnreportedCapability()
    {
        var model = CreateModel(
            "no-vision-model",
            null,
            [new CapabilityStateEntry(HiveCapabilityKeys.Vision, CapabilityState.Unsupported)]);

        // An unreported capability must be distinguishable from "Unsupported".
        Assert.True(ModelInformationFilter.Matches(
            model,
            Capability(HiveCapabilityKeys.Reasoning, ModelCapabilityFilterState.Unknown)));

        Assert.False(ModelInformationFilter.Matches(
            model,
            Capability(HiveCapabilityKeys.Vision, ModelCapabilityFilterState.Unknown)));
    }

    [Fact]
    public void CapabilityFilter_AnyStateWithSelectedCapabilityMatchesEverything()
    {
        var model = CreateModel(
            "vision-model",
            null,
            [new CapabilityStateEntry(HiveCapabilityKeys.Vision, CapabilityState.Unsupported)]);

        Assert.True(ModelInformationFilter.Matches(
            model,
            Capability(HiveCapabilityKeys.Vision, ModelCapabilityFilterState.Any)));
    }

    [Fact]
    public void PriceAndCapabilityCriteria_AreBothApplied()
    {
        var model = CreateModel(
            "expensive-vision-model",
            [TokenPrice("input_token", 3m), TokenPrice("output_token", 9m)],
            [new CapabilityStateEntry(HiveCapabilityKeys.Vision, CapabilityState.Supported)]);

        var affordableVision = new ModelFilterCriteria(
            0m,
            10m,
            IsFullPriceRange: false,
            HiveCapabilityKeys.Vision,
            ModelCapabilityFilterState.Supported);

        var unaffordableVision = new ModelFilterCriteria(
            0m,
            1m,
            IsFullPriceRange: false,
            HiveCapabilityKeys.Vision,
            ModelCapabilityFilterState.Supported);

        Assert.True(ModelInformationFilter.Matches(model, affordableVision));
        Assert.False(ModelInformationFilter.Matches(model, unaffordableVision));
    }

    [Fact]
    public void Matches_RejectsNullModel()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ModelInformationFilter.Matches(
                (ProviderModelMetadata)null!,
                FullRange()));

        Assert.Throws<ArgumentNullException>(() =>
            ModelInformationFilter.Matches(
                (ModelCatalogEntry)null!,
                FullRange()));
    }

    private static ModelFilterCriteria FullRange() =>
        new(
            MinimumPricePerMillion: 0m,
            MaximumPricePerMillion: 5m,
            IsFullPriceRange: true,
            CapabilityKey: null,
            CapabilityState: ModelCapabilityFilterState.Any);

    private static ModelFilterCriteria FreeOnlyCriteria() =>
        new(
            MinimumPricePerMillion: 0m,
            MaximumPricePerMillion: 0m,
            IsFullPriceRange: false,
            CapabilityKey: null,
            CapabilityState: ModelCapabilityFilterState.Any);

    private static ModelFilterCriteria Capability(
        CapabilityKey capabilityKey,
        ModelCapabilityFilterState state) =>
        FullRange() with
        {
            CapabilityKey = capabilityKey,
            CapabilityState = state
        };

    private static readonly DateTimeOffset Timestamp =
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static ProviderModelPrice TokenPrice(
        string billingUnit,
        decimal price) =>
        new(billingUnit, price, "USD", 1_000_000m);

    private static ProviderModelPrice PerTokenPrice(
        string billingUnit,
        decimal price) =>
        new(billingUnit, price, "USD", 1m);

    private static ProviderModelMetadata CreateModel(
        string modelId,
        IReadOnlyList<ProviderModelPrice>? prices,
        IReadOnlyList<CapabilityStateEntry>? capabilities = null) =>
        new(
            modelId,
            "example-provider",
            Timestamp,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            capabilities ?? [],
            ["text"],
            ["text"],
            pricing: prices is null ? null : new ProviderModelPricing(prices));
}