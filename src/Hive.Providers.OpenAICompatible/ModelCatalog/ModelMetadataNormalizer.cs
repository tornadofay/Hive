using System.Collections.ObjectModel;
using Hive.Core;
using System.Text.Json;

namespace Hive.Providers.OpenAICompatible.ModelCatalog;

/// <summary>
/// The single owner of OpenAI-compatible provider model-metadata
/// normalization: capabilities, modalities, thinking, limits, pricing,
/// health, and bounded/redacted provider extension data.
/// </summary>
/// <remarks>
/// These are pure JSON-to-contract functions. They own no transport
/// state and no provider-format knowledge, so every catalog parser shares
/// exactly one normalization rule.
/// </remarks>
internal static class ModelMetadataNormalizer
{
    internal static IReadOnlyList<OpenAICompatibleCapabilityDescriptor> ParseCapabilities(
        JsonElement model)
    {
        var states = new Dictionary<string, CapabilityState>(
            StringComparer.Ordinal);

        AddBooleanCapability(model, "supports_vision", "vision", states);
        AddBooleanCapability(model, "supports_tools", "tool.calling", states);
        AddBooleanCapability(model, "supports_function_calling", "tool.calling", states);
        AddBooleanCapability(model, "supports_structured_output", "structured.output", states);
        AddBooleanCapability(model, "supports_reasoning", "reasoning", states);
        AddBooleanCapability(model, "supports_thinking", "thinking", states);

        AddCapabilityFromPresence(model, "reasoning_effort", "reasoning", states);
        AddCapabilityFromPresence(model, "supported_reasoning_efforts", "reasoning", states);

        if (model.TryGetProperty("thinking", out var thinking))
        {
            if (thinking.ValueKind == JsonValueKind.False)
            {
                MergeCapabilityState(states, "thinking", CapabilityState.Unsupported);
            }
            else if (thinking.ValueKind is JsonValueKind.True or JsonValueKind.Object or JsonValueKind.Array)
            {
                MergeCapabilityState(states, "thinking", CapabilityState.Supported);
            }
        }

        if (model.TryGetProperty("capabilities", out var capabilities))
        {
            if (capabilities.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in capabilities.EnumerateObject())
                {
                    var key = NormalizeCapabilityKey(property.Name);
                    if (key is null)
                        continue;

                    var state = ParseCapabilityState(property.Value);
                    MergeCapabilityState(states, key, state);
                }
            }
            else if (capabilities.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in capabilities.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String)
                        continue;

                    var key = NormalizeCapabilityKey(item.GetString() ?? string.Empty);
                    if (key is not null)
                    {
                        MergeCapabilityState(
                            states,
                            key,
                            CapabilityState.Supported);
                    }
                }
            }
        }

        if (model.TryGetProperty("reasoning", out var reasoningObject) &&
            reasoningObject.ValueKind == JsonValueKind.Object &&
            reasoningObject.TryGetProperty("mandatory", out var mandatory) &&
            mandatory.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            // A mandatory-reasoning model always runs in reasoning mode, so this
            // is reported evidence rather than an unreported capability.
            MergeCapabilityState(
                states,
                "reasoning",
                mandatory.ValueKind == JsonValueKind.True
                    ? CapabilityState.Supported
                    : CapabilityState.Unsupported);
        }

        if (model.TryGetProperty("architecture", out var architecture) &&
            architecture.ValueKind == JsonValueKind.Object)
        {
            foreach (var modality in ParseStringList(
                         architecture,
                         "input_modalities"))
            {
                if (string.Equals(
                        modality,
                        "image",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MergeCapabilityState(
                        states,
                        "vision",
                        CapabilityState.Supported);
                }
            }

            foreach (var modality in ParseStringList(
                         architecture,
                         "output_modalities"))
            {
                if (string.Equals(
                        modality,
                        "text",
                        StringComparison.OrdinalIgnoreCase))
                {
                    MergeCapabilityState(
                        states,
                        "text.generate",
                        CapabilityState.Supported);
                }
            }
        }

        if (model.TryGetProperty("supported_parameters", out var supportedParameters) &&
            supportedParameters.ValueKind == JsonValueKind.Array)
        {
            foreach (var parameter in supportedParameters.EnumerateArray())
            {
                if (parameter.ValueKind != JsonValueKind.String)
                    continue;

                var key = NormalizeCapabilityKey(
                    parameter.GetString() ?? string.Empty);

                if (key is null)
                    continue;

                MergeCapabilityState(
                    states,
                    key,
                    CapabilityState.Supported);
            }
        }

        return states
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
                new OpenAICompatibleCapabilityDescriptor(
                    pair.Key,
                    pair.Value))
            .ToArray();
    }

    internal static void AddBooleanCapability(
        JsonElement model,
        string propertyName,
        string capabilityKey,
        Dictionary<string, CapabilityState> states)
    {
        if (!model.TryGetProperty(propertyName, out var value))
            return;

        if (value.ValueKind == JsonValueKind.True)
        {
            MergeCapabilityState(
                states,
                capabilityKey,
                CapabilityState.Supported);
        }
        else if (value.ValueKind == JsonValueKind.False)
        {
            MergeCapabilityState(
                states,
                capabilityKey,
                CapabilityState.Unsupported);
        }
    }

    internal static void AddCapabilityFromPresence(
        JsonElement model,
        string propertyName,
        string capabilityKey,
        Dictionary<string, CapabilityState> states)
    {
        if (!model.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        MergeCapabilityState(
            states,
            capabilityKey,
            CapabilityState.Supported);
    }

    internal static void MergeCapabilityState(
        Dictionary<string, CapabilityState> states,
        string capabilityKey,
        CapabilityState discoveredState)
    {
        if (!states.TryGetValue(capabilityKey, out var existingState))
        {
            states[capabilityKey] = discoveredState;
            return;
        }

        if (existingState == discoveredState)
            return;

        states[capabilityKey] = CapabilityState.Unknown;
    }

    internal static CapabilityState ParseCapabilityState(
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.True)
            return CapabilityState.Supported;

        if (value.ValueKind == JsonValueKind.False)
            return CapabilityState.Unsupported;

        if (value.ValueKind != JsonValueKind.String)
            return CapabilityState.Unknown;

        return value.GetString()?.Trim().ToLowerInvariant() switch
        {
            "supported" or "support" or "true" or "yes" => CapabilityState.Supported,
            "unsupported" or "false" or "no" => CapabilityState.Unsupported,
            _ => CapabilityState.Unknown
        };
    }

    internal static string? NormalizeCapabilityKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value
            .Trim()
            .ToLowerInvariant()
            .Replace('_', '.')
            .Replace('-', '.')
            .Replace('/', '.')
            .Replace(' ', '.');

        normalized = string.Join(
            ".",
            normalized.Split(
                '.',
                StringSplitOptions.RemoveEmptyEntries));

        return normalized switch
        {
            "text.generate" or
            "text.generation" or
            "chat" or
            "chat.completions" or
            "completion" or
            "completions" => "text.generate",

            "vision" or
            "vision.input" or
            "image.input" or
            "multimodal.vision" => "vision",

            "structured.output" or
            "structured.outputs" or
            "json.schema" or
            "json.mode" => "structured.output",

            "tool.calling" or
            "tool.call" or
            "tools" or
            "function.calling" or
            "function.calls" or
            "function.call" => "tool.calling",

            "reasoning" or
            "reasoning.support" or
            "reasoning.effort" or
            "reasoning_effort" => "reasoning",

            "thinking" or
            "thinking.level" or
            "thinking.budget" => "thinking",

            _ => null
        };
    }

    internal static IReadOnlyList<string> ParseArchitectureModalities(
        JsonElement model,
        string propertyName)
    {
        if (!model.TryGetProperty(
                "architecture",
                out var architecture) ||
            architecture.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<string>();
        }

        return ParseStringList(
            architecture,
            propertyName);
    }

    internal static IReadOnlyList<string> ParseStringList(
        JsonElement model,
        params string[] propertyNames)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var propertyName in propertyNames)
        {
            if (!model.TryGetProperty(propertyName, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var value = item.GetString();
                        if (!string.IsNullOrWhiteSpace(value) && value.Length <= 64)
                            values.Add(value.Trim());
                    }
                }
            }
            else if (property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value) && value.Length <= 64)
                    values.Add(value.Trim());
            }

            if (values.Count > 0)
                break;
        }

        if (values.Count == 0 &&
            model.TryGetProperty("modalities", out var modalities) &&
            modalities.ValueKind == JsonValueKind.Object)
        {
            var nestedPropertyNames =
                propertyNames.Any(
                    name => name.Contains("input", StringComparison.OrdinalIgnoreCase))
                    ? new[] { "input", "inputs" }
                    : new[] { "output", "outputs" };

            foreach (var name in nestedPropertyNames)
                AddStringValues(modalities, name, values);
        }

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    internal static (IReadOnlyList<string> Options, string? Default) ParseThinking(
        JsonElement model)
    {
        var options = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? defaultValue = null;

        foreach (var propertyName in new[]
        {
            "thinking_options",
            "thinking_levels",
            "supported_thinking_levels",
            "supported_reasoning_efforts"
        })
        {
            AddOrderedStringValues(model, propertyName, options, seen);
        }

        if (model.TryGetProperty("thinking", out var thinking) &&
            thinking.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "options", "levels", "supported_levels" })
                AddOrderedStringValues(thinking, propertyName, options, seen);

            defaultValue =
                TryGetString(thinking, "default") ??
                TryGetString(thinking, "default_level");
        }

        // OpenRouter reports reasoning as a nested object rather than the
        // top-level arrays or a "thinking" object:
        //   "reasoning": { "mandatory": false,
        //                   "supported_efforts": ["low","medium","high"],
        //                   "default_effort": "medium" }
        // Without this the effort levels a user can choose were lost.
        if (model.TryGetProperty("reasoning", out var reasoning) &&
            reasoning.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[]
                     {
                         "supported_efforts",
                         "efforts",
                         "options",
                         "levels"
                     })
            {
                AddOrderedStringValues(reasoning, propertyName, options, seen);
            }

            defaultValue ??=
                TryGetString(reasoning, "default_effort") ??
                TryGetString(reasoning, "default");
        }

        defaultValue ??=
            TryGetString(model, "default_thinking_level") ??
            TryGetString(model, "default_reasoning_effort");

        var reasoningEffort = TryGetString(model, "reasoning_effort");
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
            AddOrderedStringValue(reasoningEffort, options, seen);

        return (
            options
                .Take(32)
                .ToArray(),
            string.IsNullOrWhiteSpace(defaultValue)
                ? null
                : defaultValue.Trim());
    }

    internal static void AddOrderedStringValues(
        JsonElement element,
        string propertyName,
        List<string> target,
        HashSet<string> seen)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return;

        if (property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value) &&
                        value.Length <= 128)
                    {
                        AddOrderedStringValue(value.Trim(), target, seen);
                    }
                }
            }
        }
        else if (property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value) &&
                value.Length <= 128)
            {
                AddOrderedStringValue(value.Trim(), target, seen);
            }
        }
    }

    internal static void AddOrderedStringValue(
        string value,
        List<string> target,
        HashSet<string> seen)
    {
        if (seen.Add(value))
            target.Add(value);
    }

    internal static void AddStringValues(
        JsonElement element,
        string propertyName,
        HashSet<string> target)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return;

        if (property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value) && value.Length <= 128)
                        target.Add(value.Trim());
                }
            }
        }
        else if (property.ValueKind == JsonValueKind.String)
        {
            var value = property.GetString();
            if (!string.IsNullOrWhiteSpace(value) && value.Length <= 128)
                target.Add(value.Trim());
        }
    }

    internal static ProviderModelLimits? ParseLimits(JsonElement model)
    {
        var context = FirstNonNegativeInt64(
            model,
            "context_window_tokens",
            "context_window",
            "context_length",
            "max_context_tokens");

        var maxInput = FirstNonNegativeInt64(
            model,
            "max_input_tokens",
            "max_prompt_tokens");

        var maxOutput = FirstNonNegativeInt64(
            model,
            "max_output_tokens",
            "max_completion_tokens",
            "max_output_length",
            "max_tokens");

        var additional = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

        if (model.TryGetProperty("limits", out var limits) &&
            limits.ValueKind == JsonValueKind.Object)
        {
            context ??= FirstNonNegativeInt64(
                limits,
                "context_window_tokens",
                "context_window",
                "context_length",
                "max_context_tokens");

            maxInput ??= FirstNonNegativeInt64(
                limits,
                "max_input_tokens",
                "max_prompt_tokens");

            maxOutput ??= FirstNonNegativeInt64(
                limits,
                "max_output_tokens",
                "max_completion_tokens",
                "max_output_length",
                "max_tokens");

            foreach (var property in limits.EnumerateObject())
            {
                if (property.NameEquals("context_window_tokens") ||
                    property.NameEquals("context_window") ||
                    property.NameEquals("context_length") ||
                    property.NameEquals("max_context_tokens") ||
                    property.NameEquals("max_input_tokens") ||
                    property.NameEquals("max_prompt_tokens") ||
                    property.NameEquals("max_output_tokens") ||
                    property.NameEquals("max_completion_tokens") ||
                    property.NameEquals("max_output_length") ||
                    property.NameEquals("max_tokens"))
                {
                    continue;
                }

                if (additional.Count >= 32)
                    break;

                if (TrySanitizeExtensionValue(
                        property.Value,
                        property.Name,
                        depth: 0,
                        out var sanitized))
                {
                    additional[property.Name] = sanitized;
                }
            }
        }

        if (model.TryGetProperty("top_provider", out var topProvider) &&
            topProvider.ValueKind == JsonValueKind.Object)
        {
            context ??= FirstNonNegativeInt64(
                topProvider,
                "context_window_tokens",
                "context_window",
                "context_length",
                "max_context_tokens");

            maxInput ??= FirstNonNegativeInt64(
                topProvider,
                "max_input_tokens",
                "max_prompt_tokens");

            maxOutput ??= FirstNonNegativeInt64(
                topProvider,
                "max_output_tokens",
                "max_completion_tokens",
                "max_output_length",
                "max_tokens");
        }

        if (context is null && maxInput is null && maxOutput is null && additional.Count == 0)
            return null;

        return new ProviderModelLimits(
            context,
            maxInput,
            maxOutput,
            additional);
    }

    internal static long? FirstNonNegativeInt64(
        JsonElement element,
        params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (!element.TryGetProperty(name, out var property))
                continue;

            if (property.ValueKind == JsonValueKind.Number &&
                property.TryGetInt64(out var number) &&
                number >= 0)
            {
                return number;
            }

            if (property.ValueKind == JsonValueKind.String &&
                long.TryParse(
                    property.GetString(),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out number) &&
                number >= 0)
            {
                return number;
            }
        }

        return null;
    }

    internal static ProviderModelPricing? ParsePricing(
        JsonElement model,
        string? defaultPricingCurrency = null,
        decimal? defaultTokenUnitQuantity = null)
    {
        var prices = new Dictionary<string, ProviderModelPrice>(StringComparer.Ordinal);
        var variants = new List<ProviderModelPricingVariant>();
        var explicitFree = false;

        if (model.TryGetProperty("free", out var free) &&
            free.ValueKind == JsonValueKind.True)
        {
            explicitFree = true;
        }

        if (model.TryGetProperty("is_free", out var isFree) &&
            isFree.ValueKind == JsonValueKind.True)
        {
            explicitFree = true;
        }

        JsonElement pricing = default;
        if (model.TryGetProperty("pricing", out var pricingElement) &&
            pricingElement.ValueKind == JsonValueKind.Object)
        {
            pricing = pricingElement;
        }
        else if (model.TryGetProperty("prices", out var pricesElement) &&
                 pricesElement.ValueKind == JsonValueKind.Object)
        {
            pricing = pricesElement;
        }

        var defaultCurrency =
            pricing.ValueKind == JsonValueKind.Object
                ? TryGetString(pricing, "currency") ?? defaultPricingCurrency
                : defaultPricingCurrency;

        var pricingDefaultTokenUnitQuantity =
            pricing.ValueKind == JsonValueKind.Object
                ? ResolvePricingUnitQuantity(
                    pricing,
                    defaultTokenUnitQuantity)
                : defaultTokenUnitQuantity;

        if (pricing.ValueKind == JsonValueKind.Object)
        {
            if (pricing.TryGetProperty("free", out free) &&
                free.ValueKind == JsonValueKind.True)
            {
                explicitFree = true;
            }

            ParsePricingEntries(
                pricing,
                prices,
                defaultCurrency,
                pricingDefaultTokenUnitQuantity);
        }

        if (pricing.ValueKind == JsonValueKind.Object &&
            pricing.TryGetProperty("variants", out var variantsElement) &&
            variantsElement.ValueKind == JsonValueKind.Object)
        {
            var explicitDefaultKeys = variantsElement
                .EnumerateObject()
                .Where(static variant =>
                    variant.Value.ValueKind == JsonValueKind.Object &&
                    variant.Value.TryGetProperty(
                        "default",
                        out var defaultElement) &&
                    defaultElement.ValueKind == JsonValueKind.True)
                .Select(static variant => variant.Name)
                .ToArray();

            string? explicitDefaultKey =
                explicitDefaultKeys.Length == 1
                    ? explicitDefaultKeys[0]
                    : null;

            var implicitDefaultKeys = variantsElement
                .EnumerateObject()
                .Where(static variant =>
                    string.Equals(
                        variant.Name,
                        "default",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        variant.Name,
                        "standard",
                        StringComparison.OrdinalIgnoreCase))
                .Select(static variant => variant.Name)
                .ToArray();

            if (explicitDefaultKeys.Length == 0 &&
                implicitDefaultKeys.Length == 1)
            {
                explicitDefaultKey = implicitDefaultKeys[0];
            }

            foreach (var variantProperty in variantsElement.EnumerateObject())
            {
                if (variantProperty.Value.ValueKind != JsonValueKind.Object)
                    continue;

                var variant = variantProperty.Value;
                var variantCurrency =
                    TryGetString(variant, "currency") ??
                    defaultCurrency;

                var variantPrices = new Dictionary<string, ProviderModelPrice>(
                    StringComparer.Ordinal);

                var nestedPricing = variant;
                if (variant.TryGetProperty("pricing", out var nestedPricingElement) &&
                    nestedPricingElement.ValueKind == JsonValueKind.Object)
                {
                    nestedPricing = nestedPricingElement;
                }
                else if (variant.TryGetProperty("prices", out var nestedPricesElement) &&
                         nestedPricesElement.ValueKind == JsonValueKind.Object)
                {
                    nestedPricing = nestedPricesElement;
                }

                ParsePricingEntries(
                    nestedPricing,
                    variantPrices,
                    variantCurrency,
                    ResolvePricingUnitQuantity(
                        nestedPricing,
                        defaultTokenUnitQuantity));

                if (variant.TryGetProperty("free", out var variantFree) &&
                    variantFree.ValueKind == JsonValueKind.True)
                {
                    explicitFree = true;
                }

                var isDefault =
                    explicitDefaultKey is not null &&
                    string.Equals(
                        variantProperty.Name,
                        explicitDefaultKey,
                        StringComparison.OrdinalIgnoreCase);

                variants.Add(
                    new ProviderModelPricingVariant(
                        variantProperty.Name,
                        variantPrices.Values
                            .OrderBy(item => item.BillingUnit, StringComparer.Ordinal)
                            .ToArray(),
                        ParsePricingVariantConditions(variant),
                        isDefault));
            }
        }

        // OpenRouter reports tiered pricing as an ARRAY of threshold overrides rather
// than the object-shaped "variants" form, for example:
//   "pricing": { "prompt": "0.000002", "completion": "0.00001",
//                 "overrides": [{ "min_prompt_tokens": 272000,
//                                 "prompt": "0.000004" }] }
// Only the object form was recognized, so those tiers were silently discarded
// and a user saw only the base rate even for long-context requests.
if (pricing.ValueKind == JsonValueKind.Object &&
            pricing.TryGetProperty("overrides", out var overrides) &&
            overrides.ValueKind == JsonValueKind.Array)
        {
            ParsePricingOverrides(
                overrides,
                defaultCurrency,
                defaultTokenUnitQuantity,
                variants);
        }

        if (prices.Count == 0 && variants.Count > 0)
        {
            var defaultVariants = variants
                .Where(static variant => variant.IsDefault)
                .ToArray();

            if (defaultVariants.Length == 1)
            {
                prices = defaultVariants[0].Prices.ToDictionary(
                    price => price.BillingUnit,
                    StringComparer.Ordinal);
            }
            else if (variants.Count == 1)
            {
                prices = variants[0].Prices.ToDictionary(
                    price => price.BillingUnit,
                    StringComparer.Ordinal);
            }
        }

        var pricingModel = new ProviderModelPricing(
            prices.Values
                .OrderBy(item => item.BillingUnit, StringComparer.Ordinal)
                .ToArray(),
            explicitFree,
            variants);

        if (!explicitFree &&
            HasExplicitZeroInputOutputTokenPricing(pricingModel.Prices))
        {
            explicitFree = true;
        }

        if (prices.Count == 0 &&
            variants.Count == 0 &&
            !explicitFree)
        {
            return null;
        }

        return new ProviderModelPricing(
            prices.Values
                .OrderBy(item => item.BillingUnit, StringComparer.Ordinal)
                .ToArray(),
            explicitFree,
            variants);
    }

    internal static bool HasExplicitZeroInputOutputTokenPricing(
        IReadOnlyList<ProviderModelPrice> prices)
    {
        var input = prices.FirstOrDefault(
            static price =>
                string.Equals(
                    price.BillingUnit,
                    "input_token",
                    StringComparison.OrdinalIgnoreCase));

        var output = prices.FirstOrDefault(
            static price =>
                string.Equals(
                    price.BillingUnit,
                    "output_token",
                    StringComparison.OrdinalIgnoreCase));

        return input is not null &&
               output is not null &&
               input.Price == 0m &&
               output.Price == 0m;
    }

    internal static decimal? ResolvePricingUnitQuantity(
        JsonElement pricing,
        decimal? fallback)
    {
        foreach (var propertyName in new[] { "unit_quantity", "quantity", "units" })
        {
            if (!pricing.TryGetProperty(propertyName, out var value))
                continue;

            var explicitQuantity = TryGetDecimal(value);
            return explicitQuantity is > 0m
                ? explicitQuantity
                : null;
        }

        var unit = TryGetString(pricing, "unit");
        return unit is null
            ? fallback
            : TryGetUnitQuantity(unit) ?? fallback;
    }

    internal static void ParsePricingEntries(
        JsonElement pricing,
        IDictionary<string, ProviderModelPrice> prices,
        string? defaultCurrency,
        decimal? defaultTokenUnitQuantity)
    {
        if (pricing.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in pricing.EnumerateObject())
        {
            if (string.Equals(property.Name, "currency", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "unit", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "unit_quantity", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "free", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "variants", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "conditions", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(property.Name, "default", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            AddPrice(
                prices,
                property.Name,
                property.Value,
                defaultCurrency,
                defaultTokenUnitQuantity);
        }
    }

    internal static IReadOnlyDictionary<string, string> ParsePricingVariantConditions(
        JsonElement variant)
    {
        if (!variant.TryGetProperty("conditions", out var conditions) ||
            conditions.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var property in conditions.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                continue;

            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
                continue;

            result[property.Name] = value.Trim();

            if (result.Count >= 16)
                break;
        }

        return result;
    }

    internal static void AddPrice(
        IDictionary<string, ProviderModelPrice> prices,
        string propertyName,
        JsonElement value,
        string? defaultCurrency,
        decimal? defaultTokenUnitQuantity)
    {
        decimal? price = null;
        string? currency = defaultCurrency;
        decimal? unitQuantity = null;
        var unit = NormalizePricingUnit(propertyName);

        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
        {
            price = TryGetDecimal(value);
            unitQuantity = defaultTokenUnitQuantity;
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            price = TryGetDecimalProperty(value, "price", "amount", "value");
            currency = TryGetString(value, "currency") ?? defaultCurrency;
            var explicitUnit = TryGetString(value, "unit");
            if (explicitUnit is not null)
            {
                var normalizedExplicitUnit = NormalizePricingUnit(explicitUnit);
                if (IsKnownBillingDimension(normalizedExplicitUnit))
                    unit = normalizedExplicitUnit;
            }

            unitQuantity = TryGetDecimalProperty(
                value,
                "unit_quantity",
                "quantity",
                "units");

            if (unitQuantity is null && explicitUnit is not null)
            {
                unitQuantity = TryGetUnitQuantity(explicitUnit);
            }

            if (unitQuantity is null)
            {
                unitQuantity = TryGetUnitQuantity(unit)
                    ?? defaultTokenUnitQuantity;
            }
        }

        if (price is null || price < 0)
            return;

        if (unitQuantity is <= 0)
            unitQuantity = null;

        var normalized = new ProviderModelPrice(
            unit,
            price.Value,
            currency,
            unitQuantity);

        prices.TryAdd(normalized.BillingUnit, normalized);
    }

    internal static decimal? TryGetDecimalProperty(
        JsonElement element,
        params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value))
                continue;

            var result = TryGetDecimal(value);
            if (result is not null)
                return result;
        }

        return null;
    }

    internal static decimal? TryGetDecimal(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number &&
            value.TryGetDecimal(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String &&
            decimal.TryParse(
                value.GetString(),
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out number))
        {
            return number;
        }

        return null;
    }

    internal static string NormalizePricingUnit(string propertyName)
    {
        var normalized = propertyName.Trim().ToLowerInvariant();

        return normalized switch
        {
            "prompt" or "input" or "input_tokens" or "prompt_tokens" or "input_token"
                => "input_token",
            "completion" or "output" or "output_tokens" or "completion_tokens" or "output_token"
                => "output_token",
            "reasoning" or "reasoning_tokens" or "reasoning_token"
                => "reasoning_token",
            "cache_read" or "cached_input" or "cache_input" or "cached_input_token"
                => "cached_input_token",
            "cache_write" or "cached_output" or "cache_output" or "cached_output_token"
                => "cached_output_token",
            _ => normalized.Replace('-', '_').Replace(' ', '_')
        };
    }

    internal static bool IsKnownBillingDimension(string value) =>
        string.Equals(value, "input_token", StringComparison.Ordinal) ||
        string.Equals(value, "output_token", StringComparison.Ordinal) ||
        string.Equals(value, "reasoning_token", StringComparison.Ordinal) ||
        string.Equals(value, "cached_input_token", StringComparison.Ordinal) ||
        string.Equals(value, "cached_output_token", StringComparison.Ordinal);

    internal static decimal? TryGetUnitQuantity(string unit)
    {
        var normalized = unit
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        return normalized switch
        {
            "token" or
            "tokens" or
            "per_token" or
            "per_tokens" => 1m,

            "1k" or
            "1k_token" or
            "1k_tokens" or
            "per_1k" or
            "per_1k_token" or
            "per_1k_tokens" or
            "per_1000" or
            "per_1000_token" or
            "per_1000_tokens" => 1_000m,

            "1m" or
            "1m_token" or
            "1m_tokens" or
            "per_1m" or
            "per_1m_token" or
            "per_1m_tokens" or
            "per_1000000" or
            "per_1000000_token" or
            "per_1000000_tokens" => 1_000_000m,

            _ => null
        };
    }

    internal static IReadOnlyDictionary<string, JsonElement> ParseExtensionData(
        JsonElement model)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id",
            "object",
            "owned_by",
            "name",
            "display_name",
            "displayName",
            "description",
            "model_description",
            "modelDescription",
            "created",
            "available",
            "health",
            "state",
            "status",
            "lifecycle_state",
            "model_state",
            "family",
            "model_family",
            "modelFamily",
            "type",
            "model_type",
            "modelType",
            "category",
            "model_category",
            "modelCategory",
            "version",
            "model_version",
            "modelVersion",
            "input_modalities",
            "inputModalities",
            "modalities_input",
            "supported_input_modalities",
            "modalities",
            "output_modalities",
            "outputModalities",
            "modalities_output",
            "supported_output_modalities",
            "thinking_options",
            "thinking_levels",
            "supported_thinking_levels",
            "supported_reasoning_efforts",
            "default_thinking_level",
            "default_reasoning_effort",
            "reasoning_effort",
            "thinking",
            "supports_vision",
            "supports_tools",
            "supports_function_calling",
            "supports_structured_output",
            "supports_reasoning",
            "supports_thinking",
            "limits",
            "context_window_tokens",
            "context_window",
            "context_length",
            "max_context_tokens",
            "max_input_tokens",
            "max_prompt_tokens",
            "max_output_tokens",
            "max_completion_tokens",
            "max_output_length",
            "max_tokens",
            "pricing",
            "prices",
            "free",
            "is_free"
        };

        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var totalBytes = 0;

        foreach (var property in model.EnumerateObject())
        {
            if (known.Contains(property.Name) ||
                IsSensitivePropertyName(property.Name) ||
                values.Count >= 128)
            {
                continue;
            }

            if (!TrySanitizeExtensionValue(
                    property.Value,
                    property.Name,
                    depth: 0,
                    out var sanitized))
            {
                continue;
            }

            var raw = sanitized.GetRawText();
            if (raw.Length > 16 * 1024)
                continue;

            totalBytes += raw.Length;
            if (totalBytes > 64 * 1024)
                break;

            values[property.Name.Trim()] = sanitized;
        }

        return new ReadOnlyDictionary<string, JsonElement>(values);
    }

    internal static bool TrySanitizeExtensionValue(
        JsonElement value,
        string propertyName,
        int depth,
        out JsonElement sanitized)
    {
        sanitized = default;

        if (depth > 4 || IsSensitivePropertyName(propertyName))
            return false;

        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
            {
                var objectValues = new Dictionary<string, JsonElement>(StringComparer.Ordinal);

                foreach (var property in value.EnumerateObject())
                {
                    if (objectValues.Count >= 32 ||
                        IsSensitivePropertyName(property.Name) ||
                        !TrySanitizeExtensionValue(
                            property.Value,
                            property.Name,
                            depth + 1,
                            out var child))
                    {
                        continue;
                    }

                    objectValues[property.Name] = child;
                }

                sanitized = JsonSerializer.SerializeToElement(objectValues);
                return true;
            }

            case JsonValueKind.Array:
            {
                var arrayValues = new List<JsonElement>();

                foreach (var item in value.EnumerateArray())
                {
                    if (arrayValues.Count >= 32 ||
                        !TrySanitizeExtensionValue(
                            item,
                            propertyName,
                            depth + 1,
                            out var child))
                    {
                        continue;
                    }

                    arrayValues.Add(child);
                }

                sanitized = JsonSerializer.SerializeToElement(arrayValues);
                return true;
            }

            case JsonValueKind.String:
            {
                var text = value.GetString();
                if (string.IsNullOrWhiteSpace(text) || text.Length > 2048)
                    return false;

                text = text.Trim();
                if (ContainsCredentialBearingUri(text))
                    return false;

                sanitized = JsonSerializer.SerializeToElement(text);
                return true;
            }

            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
            case JsonValueKind.Null:
                sanitized = value.Clone();
                return true;

            default:
                return false;
        }
    }

    internal static bool IsSensitivePropertyName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return normalized == "authorization" ||
               normalized.EndsWith("_authorization", StringComparison.Ordinal) ||
               normalized == "api_key" ||
               normalized == "apikey" ||
               normalized.EndsWith("_api_key", StringComparison.Ordinal) ||
               normalized.EndsWith("_apikey", StringComparison.Ordinal) ||
               normalized == "access_token" ||
               normalized == "refresh_token" ||
               normalized == "id_token" ||
               normalized == "token" ||
               normalized.EndsWith("_token", StringComparison.Ordinal) ||
               normalized == "secret" ||
               normalized.EndsWith("_secret", StringComparison.Ordinal) ||
               normalized == "password" ||
               normalized.EndsWith("_password", StringComparison.Ordinal) ||
               normalized == "credential" ||
               normalized.EndsWith("_credential", StringComparison.Ordinal) ||
               normalized == "privatekey" ||
               normalized == "private_key" ||
               normalized == "clientsecret" ||
               normalized == "client_secret" ||
               normalized == "cookie" ||
               normalized.EndsWith("_cookie", StringComparison.Ordinal) ||
               normalized == "webhook" ||
               normalized.EndsWith("_webhook", StringComparison.Ordinal);
    }

    internal static bool ContainsCredentialBearingUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return true;

        var query = uri.GetComponents(
            UriComponents.Query,
            UriFormat.UriEscaped);

        foreach (var part in query.Split(
                     ['&', ';'],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = part.IndexOf('=');
            var name = equals >= 0 ? part[..equals] : part;
            name = Uri.UnescapeDataString(name).Trim();

            if (name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "api-key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "api_key", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "apikey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "authorization", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    internal static ProviderHealthStatus ParseHealth(JsonElement model)
    {
        if (!model.TryGetProperty("health", out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return ProviderHealthStatus.Unknown;
        }

        return value.GetString()?.Trim().ToLowerInvariant() switch
        {
            "healthy" => ProviderHealthStatus.Healthy,
            "degraded" or "degrading" => ProviderHealthStatus.Degraded,
            "unhealthy" or "failed" => ProviderHealthStatus.Unhealthy,
            _ => ProviderHealthStatus.Unknown
        };
    }

    internal static string? TryGetString(
        JsonElement element,
        params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!element.TryGetProperty(propertyName, out var value) ||
                value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var result = value.GetString();

            if (!string.IsNullOrWhiteSpace(result))
                return result.Trim();
        }

        return null;
    }

    /// <summary>
    /// Parses an array of threshold pricing overrides into pricing variants.
    /// </summary>
    /// <remarks>
    /// Each entry carries its own subset of rates plus its threshold conditions. The
    /// first entry inherits the base rates so a tier is never presented as cheaper
    /// than it actually is; later entries contribute only the rates they override.
    /// </remarks>
    internal static void ParsePricingOverrides(
        JsonElement overrides,
        string? defaultCurrency,
        decimal? defaultTokenUnitQuantity,
        List<ProviderModelPricingVariant> variants)
    {
        const int maxOverrides = 8;

        var inheritedRates = default(JsonElement);
        var isFirstEntry = true;

        foreach (var entry in overrides.EnumerateArray())
        {
            if (variants.Count >= maxOverrides)
                return;

            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var mergedRates = new Dictionary<string, JsonElement>(
                StringComparer.OrdinalIgnoreCase);

            if (inheritedRates.ValueKind == JsonValueKind.Object)
            {
                foreach (var pair in inheritedRates.EnumerateObject())
                    mergedRates[pair.Name] = pair.Value;
            }

            var threshold = FirstNonNegativeInt64(
                entry,
                "min_prompt_tokens",
                "min_input_tokens",
                "min_tokens");

            var hasRate = false;

            foreach (var pair in entry.EnumerateObject())
            {
                if (pair.NameEquals("min_prompt_tokens") ||
                    pair.NameEquals("min_input_tokens") ||
                    pair.NameEquals("min_tokens"))
                {
                    continue;
                }

                hasRate = true;
                mergedRates[pair.Name] = pair.Value;
            }

            var tierPrices = new Dictionary<string, ProviderModelPrice>(
                StringComparer.Ordinal);

            if (hasRate)
            {
                ParsePricingEntries(
                    JsonSerializer.SerializeToElement(mergedRates),
                    tierPrices,
                    defaultCurrency,
                    defaultTokenUnitQuantity);
            }

            if (tierPrices.Count == 0)
            {
                isFirstEntry = false;
                inheritedRates = entry;
                continue;
            }

            var conditions = new Dictionary<string, string>(
                ParsePricingVariantConditions(entry),
                StringComparer.OrdinalIgnoreCase);

            if (threshold is { } minimumTokens)
            {
                conditions["min_prompt_tokens"] = minimumTokens.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            }

            var tierKey = threshold is { } value
                ? "over " + value.ToString(
                      System.Globalization.CultureInfo.InvariantCulture) +
                  " tokens"
                : isFirstEntry
                    ? "base"
                    : "override " + variants.Count.ToString(
                        System.Globalization.CultureInfo.InvariantCulture);

            try
            {
                variants.Add(
                    new ProviderModelPricingVariant(
                        tierKey,
                        tierPrices.Values
                            .OrderBy(item => item.BillingUnit, StringComparer.Ordinal)
                            .ToArray(),
                        conditions,
                        isDefault: isFirstEntry));
            }
            catch (ArgumentException)
            {
                // A malformed override tier must not discard the base pricing.
            }

            isFirstEntry = false;
            inheritedRates = entry;
        }
    }
}