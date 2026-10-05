using Hive.Core;
using System.Collections.ObjectModel;
using System.Text.Json;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelCatalogFailure;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelMetadataNormalizer;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

internal sealed class OllamaModelCatalogParser : IModelCatalogParser
{
    public Result<OpenAICompatibleModelCatalog> Parse(
        string responseJson,
        int? rateLimitRemaining)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("models", out var models) ||
                models.ValueKind != JsonValueKind.Array)
            {
                return ModelSerializationFailure();
            }

            var descriptors = new List<OpenAICompatibleModelDescriptor>();
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var model in models.EnumerateArray())
            {
                if (model.ValueKind != JsonValueKind.Object)
                    return ModelSerializationFailure();

                var id =
                    TryGetString(
                        model,
                        "name",
                        "model");

                if (string.IsNullOrWhiteSpace(id) ||
                    id.Length > OpenAICompatibleChatRequest.MaxModelLength ||
                    !ids.Add(id))
                {
                    return ModelSerializationFailure();
                }

                var details =
                    model.TryGetProperty("details", out var detailsElement) &&
                    detailsElement.ValueKind == JsonValueKind.Object
                        ? detailsElement
                        : default;

                var family = details.ValueKind == JsonValueKind.Object
                    ? TryGetString(details, "family")
                    : null;

                var modelType = details.ValueKind == JsonValueKind.Object
                    ? TryGetString(details, "format")
                    : null;

                var extensionData = ParseExtensionData(model);

                if (details.ValueKind == JsonValueKind.Object)
                {
                    var detailsExtension =
                        details.EnumerateObject()
                            .Where(property =>
                                !string.Equals(
                                    property.Name,
                                    "family",
                                    StringComparison.OrdinalIgnoreCase) &&
                                !string.Equals(
                                    property.Name,
                                    "format",
                                    StringComparison.OrdinalIgnoreCase))
                            .Take(32)
                            .ToDictionary(
                                property => property.Name,
                                property => property.Value.Clone(),
                                StringComparer.Ordinal);

                    if (detailsExtension.Count > 0)
                    {
                        var combined =
                            new Dictionary<string, JsonElement>(
                                extensionData,
                                StringComparer.Ordinal);

                        foreach (var property in detailsExtension)
                            combined.TryAdd(property.Key, property.Value);

                        extensionData =
                            new ReadOnlyDictionary<string, JsonElement>(
                                combined);
                    }
                }

                descriptors.Add(
                    new OpenAICompatibleModelDescriptor(
                        id,
                        OwnedBy: null,
                        CreatedAtUtc: null,
                        Availability: ProviderAvailabilityStatus.Available,
                        Health: ProviderHealthStatus.Unknown,
                        Capabilities: Array.Empty<OpenAICompatibleCapabilityDescriptor>(),
                        InputModalities: null,
                        OutputModalities: null,
                        DisplayName: id,
                        Description: null,
                        Family: family,
                        ModelType: modelType,
                        Category: null,
                        Version: null,
                        OperationalState: null,
                        ThinkingOptions: null,
                        DefaultThinkingLevel: null,
                        Limits: null,
                        Pricing: null,
                        ExtensionData: extensionData));
            }

            descriptors.Sort(
                static (left, right) =>
                    StringComparer.Ordinal.Compare(left.Id, right.Id));

            return Result<OpenAICompatibleModelCatalog>.Success(
                new OpenAICompatibleModelCatalog(
                    descriptors,
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
