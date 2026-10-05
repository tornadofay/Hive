using Hive.Core;
using System.Text.Json;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelCatalogFailure;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelMetadataNormalizer;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

internal sealed class GeminiModelCatalogParser : IModelCatalogParser
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
                if (model.ValueKind != JsonValueKind.Object ||
                    !model.TryGetProperty("name", out var nameElement) ||
                    nameElement.ValueKind != JsonValueKind.String)
                {
                    return ModelSerializationFailure();
                }

                var id = nameElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(id))
                    return ModelSerializationFailure();

                if (id.StartsWith("models/", StringComparison.OrdinalIgnoreCase))
                    id = id["models/".Length..];

                if (id.Length > OpenAICompatibleChatRequest.MaxModelLength ||
                    !ids.Add(id))
                {
                    return ModelSerializationFailure();
                }

                var capabilities = new Dictionary<string, CapabilityState>(
                    StringComparer.Ordinal);

                if (model.TryGetProperty(
                        "supportedGenerationMethods",
                        out var methods) &&
                    methods.ValueKind == JsonValueKind.Array)
                {
                    foreach (var method in methods.EnumerateArray())
                    {
                        if (method.ValueKind != JsonValueKind.String)
                            continue;

                        switch (method.GetString())
                        {
                            case "generateContent":
                            case "streamGenerateContent":
                                capabilities["text.generate"] =
                                    CapabilityState.Supported;
                                break;
                        }
                    }
                }

                if (model.TryGetProperty("thinking", out var thinking) &&
                    thinking.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    var state = thinking.GetBoolean()
                        ? CapabilityState.Supported
                        : CapabilityState.Unsupported;

                    capabilities["reasoning"] = state;
                    capabilities["thinking"] = state;
                }

                var inputLimit = FirstNonNegativeInt64(
                    model,
                    "inputTokenLimit");
                var outputLimit = FirstNonNegativeInt64(
                    model,
                    "outputTokenLimit");

                ProviderModelLimits? limits =
                    inputLimit is null && outputLimit is null
                        ? null
                        : new ProviderModelLimits(
                            inputLimit,
                            inputLimit,
                            outputLimit);

                var extensionData = ParseExtensionData(model);

                descriptors.Add(
                    new OpenAICompatibleModelDescriptor(
                        id,
                        OwnedBy: null,
                        CreatedAtUtc: null,
                        ProviderAvailabilityStatus.Unknown,
                        ProviderHealthStatus.Unknown,
                        capabilities
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(
                                pair => new OpenAICompatibleCapabilityDescriptor(
                                    pair.Key,
                                    pair.Value))
                            .ToArray(),
                        DisplayName: TryGetString(
                            model,
                            "displayName"),
                        Description: TryGetString(
                            model,
                            "description"),
                        Family: TryGetString(
                            model,
                            "baseModelId"),
                        ModelType: "gemini",
                        Category: null,
                        Version: TryGetString(
                            model,
                            "version"),
                        OperationalState: null,
                        ThinkingOptions: null,
                        DefaultThinkingLevel: null,
                        Limits: limits,
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
