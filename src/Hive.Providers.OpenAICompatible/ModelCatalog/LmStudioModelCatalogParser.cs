using Hive.Core;
using System.Text.Json;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelCatalogFailure;
using static Hive.Providers.OpenAICompatible.ModelCatalog.ModelMetadataNormalizer;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

internal sealed class LmStudioModelCatalogParser : IModelCatalogParser
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
                    !model.TryGetProperty("key", out var keyElement) ||
                    keyElement.ValueKind != JsonValueKind.String)
                {
                    return ModelSerializationFailure();
                }

                var id = keyElement.GetString()?.Trim();

                if (string.IsNullOrWhiteSpace(id) ||
                    id.Length > OpenAICompatibleChatRequest.MaxModelLength ||
                    !ids.Add(id))
                {
                    return ModelSerializationFailure();
                }

                var capabilities = new Dictionary<string, CapabilityState>(
                    StringComparer.Ordinal);

                if (model.TryGetProperty("capabilities", out var capabilityObject) &&
                    capabilityObject.ValueKind == JsonValueKind.Object)
                {
                    if (capabilityObject.TryGetProperty("vision", out var vision) &&
                        vision.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        capabilities["vision"] =
                            vision.GetBoolean()
                                ? CapabilityState.Supported
                                : CapabilityState.Unsupported;
                    }

                    if (capabilityObject.TryGetProperty(
                            "trained_for_tool_use",
                            out var toolUse) &&
                        toolUse.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        capabilities["tool.calling"] =
                            toolUse.GetBoolean()
                                ? CapabilityState.Supported
                                : CapabilityState.Unsupported;
                    }

                    if (capabilityObject.TryGetProperty(
                            "reasoning",
                            out var reasoning) &&
                        reasoning.ValueKind == JsonValueKind.Object)
                    {
                        capabilities["reasoning"] =
                            CapabilityState.Supported;
                    }
                }

                var thinking = ParseLmStudioThinking(
                    model);

                if (thinking.Options.Count > 0)
                    capabilities["thinking"] =
                        CapabilityState.Supported;

                var contextLimit = FirstNonNegativeInt64(
                    model,
                    "max_context_length");

                var extensionData = ParseExtensionData(model);

                descriptors.Add(
                    new OpenAICompatibleModelDescriptor(
                        id,
                        OwnedBy: TryGetString(model, "publisher"),
                        CreatedAtUtc: null,
                        Availability: ProviderAvailabilityStatus.Available,
                        Health: ProviderHealthStatus.Unknown,
                        Capabilities: capabilities
                            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                            .Select(
                                pair => new OpenAICompatibleCapabilityDescriptor(
                                    pair.Key,
                                    pair.Value))
                            .ToArray(),
                        InputModalities: null,
                        OutputModalities: null,
                        DisplayName: TryGetString(model, "display_name"),
                        Description: TryGetString(model, "description"),
                        Family: TryGetString(model, "architecture"),
                        ModelType: TryGetString(model, "type"),
                        Category: null,
                        Version: null,
                        OperationalState: null,
                        ThinkingOptions: thinking.Options,
                        DefaultThinkingLevel: thinking.Default,
                        Limits: contextLimit is null
                            ? null
                            : new ProviderModelLimits(
                                contextLimit,
                                contextLimit,
                                null),
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
    }


    internal (IReadOnlyList<string> Options, string? Default) ParseLmStudioThinking(
        JsonElement model)
    {
        if (!model.TryGetProperty("capabilities", out var capabilities) ||
            capabilities.ValueKind != JsonValueKind.Object ||
            !capabilities.TryGetProperty("reasoning", out var reasoning) ||
            reasoning.ValueKind != JsonValueKind.Object)
        {
            return (Array.Empty<string>(), null);
        }

        var options = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddOrderedStringValues(
            reasoning,
            "allowed_options",
            options,
            seen);

        var defaultValue =
            TryGetString(reasoning, "default");

        return (
            options
                .Take(32)
                .ToArray(),
            defaultValue);
    }
}
