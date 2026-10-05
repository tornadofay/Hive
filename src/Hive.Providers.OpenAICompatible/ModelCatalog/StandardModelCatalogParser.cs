using Hive.Core;
using System.Text.Json;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelCatalogFailure;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelMetadataNormalizer;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

internal sealed class StandardModelCatalogParser : IModelCatalogParser
{
    private readonly string? _defaultPricingCurrency;
    private readonly decimal? _defaultTokenUnitQuantity;

    internal StandardModelCatalogParser(
        string? defaultPricingCurrency = null,
        decimal? defaultTokenUnitQuantity = null)
    {
        _defaultPricingCurrency = defaultPricingCurrency;
        _defaultTokenUnitQuantity = defaultTokenUnitQuantity;
    }

    public Result<OpenAICompatibleModelCatalog> Parse(
        string responseJson,
        int? rateLimitRemaining) =>
        ParseModelCatalog(
            responseJson,
            rateLimitRemaining,
            _defaultPricingCurrency,
            _defaultTokenUnitQuantity);

    internal Result<OpenAICompatibleModelCatalog> ParseModelCatalog(
        string responseJson,
        int? rateLimitRemaining,
        string? defaultPricingCurrency = null,
        decimal? defaultTokenUnitQuantity = null)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("data", out var data) ||
                data.ValueKind != JsonValueKind.Array)
            {
                return ModelSerializationFailure();
            }

            var models = new List<OpenAICompatibleModelDescriptor>();
            var modelIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var model in data.EnumerateArray())
            {
                if (model.ValueKind != JsonValueKind.Object ||
                    !model.TryGetProperty("id", out var idElement) ||
                    idElement.ValueKind != JsonValueKind.String)
                {
                    return ModelSerializationFailure();
                }

                var id = idElement.GetString();
                if (string.IsNullOrWhiteSpace(id) ||
                    id.Length > OpenAICompatibleChatRequest.MaxModelLength)
                {
                    return ModelSerializationFailure();
                }

                id = id.Trim();

                if (!modelIds.Add(id))
                    return ModelSerializationFailure();

                var ownedBy = TryGetString(model, "owned_by");
                if (ownedBy is not null && ownedBy.Length > 200)
                    return ModelSerializationFailure();

                DateTimeOffset? createdAtUtc = null;
                if (model.TryGetProperty("created", out var createdElement) &&
                    createdElement.ValueKind == JsonValueKind.Number &&
                    createdElement.TryGetInt64(out var createdUnixSeconds) &&
                    createdUnixSeconds >= 0)
                {
                    try
                    {
                        createdAtUtc = DateTimeOffset.FromUnixTimeSeconds(createdUnixSeconds);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        return ModelSerializationFailure();
                    }
                }

                var availability =
                    model.TryGetProperty("available", out var availableElement) &&
                    availableElement.ValueKind == JsonValueKind.False
                        ? ProviderAvailabilityStatus.Unavailable
                        : model.TryGetProperty("available", out availableElement) &&
                          availableElement.ValueKind == JsonValueKind.True
                            ? ProviderAvailabilityStatus.Available
                            : model.TryGetProperty("active", out var activeElement) &&
                              activeElement.ValueKind == JsonValueKind.False
                                ? ProviderAvailabilityStatus.Unavailable
                                : model.TryGetProperty("active", out activeElement) &&
                                  activeElement.ValueKind == JsonValueKind.True
                                    ? ProviderAvailabilityStatus.Available
                                    : ProviderAvailabilityStatus.Unknown;

                var health = ParseHealth(model);
                var displayName = TryGetString(model, "name", "display_name", "displayName");
                var description = TryGetString(model, "description", "model_description", "modelDescription");
                var operationalState = TryGetString(model, "state", "status", "lifecycle_state", "model_state");
                var family = TryGetString(model, "family", "model_family", "modelFamily");
                var modelType = TryGetString(model, "type", "model_type", "modelType");
                var category = TryGetString(model, "category", "model_category", "modelCategory");
                var version = TryGetString(model, "version", "model_version", "modelVersion");
                var capabilities = ParseCapabilities(model);
                var inputModalities = ParseStringList(
                    model,
                    "input_modalities",
                    "inputModalities",
                    "modalities_input",
                    "supported_input_modalities");
                if (inputModalities.Count == 0)
                {
                    inputModalities = ParseArchitectureModalities(
                        model,
                        "input_modalities");
                }

                var outputModalities = ParseStringList(
                    model,
                    "output_modalities",
                    "outputModalities",
                    "modalities_output",
                    "supported_output_modalities");
                if (outputModalities.Count == 0)
                {
                    outputModalities = ParseArchitectureModalities(
                        model,
                        "output_modalities");
                }
                var thinking = ParseThinking(model);
                var limits = ParseLimits(model);
                var pricing = ParsePricing(
                    model,
                    defaultPricingCurrency,
                    defaultTokenUnitQuantity);
                var extensionData = ParseExtensionData(model);

                models.Add(
                    new OpenAICompatibleModelDescriptor(
                        id,
                        ownedBy,
                        createdAtUtc,
                        availability,
                        health,
                        capabilities,
                        inputModalities,
                        outputModalities,
                        displayName,
                        description,
                        family,
                        modelType,
                        category,
                        version,
                        operationalState,
                        thinking.Options,
                        thinking.Default,
                        limits,
                        pricing,
                        extensionData));
            }

            models.Sort(static (left, right) =>
                StringComparer.Ordinal.Compare(left.Id, right.Id));

            return Result<OpenAICompatibleModelCatalog>.Success(
                new OpenAICompatibleModelCatalog(
                    models,
                    rateLimitRemaining));
        }
        catch (JsonException)
        {
            return ModelSerializationFailure();
        }
        catch (ArgumentException)
        {
            return ModelSerializationFailure();
        }
    }}
