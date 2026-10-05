using Hive.Core;
using System.Text.Json;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelCatalogFailure;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelMetadataNormalizer;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

internal sealed class WrappedModelCatalogParser : IModelCatalogParser
{
    private readonly string _arrayPropertyName;
    private readonly StandardModelCatalogParser _innerParser;

    internal WrappedModelCatalogParser(
        string arrayPropertyName,
        string? defaultPricingCurrency = null,
        decimal? defaultTokenUnitQuantity = null)
    {
        _arrayPropertyName = arrayPropertyName;
        _innerParser = new StandardModelCatalogParser(
            defaultPricingCurrency,
            defaultTokenUnitQuantity);
    }

    public Result<OpenAICompatibleModelCatalog> Parse(
        string responseJson,
        int? rateLimitRemaining) =>
        ParseWrappedModelCatalog(
            responseJson,
            _arrayPropertyName,
            rateLimitRemaining);

    internal Result<OpenAICompatibleModelCatalog> ParseWrappedModelCatalog(
        string responseJson,
        string arrayPropertyName,
        int? rateLimitRemaining,
        string? defaultPricingCurrency = null,
        decimal? defaultTokenUnitQuantity = null)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty(arrayPropertyName, out var models) ||
                models.ValueKind != JsonValueKind.Array)
            {
                return ModelSerializationFailure();
            }

            var normalized = JsonSerializer.Serialize(
                new
                {
                    data = models
                });

            return _innerParser.ParseModelCatalog(
                normalized,
                rateLimitRemaining,
                defaultPricingCurrency,
                defaultTokenUnitQuantity);
        }
        catch (JsonException)
        {
            return ModelSerializationFailure();
        }
    }
}
