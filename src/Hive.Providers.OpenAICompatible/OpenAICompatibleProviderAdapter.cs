using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hive.Core;

namespace Hive.Providers.OpenAICompatible;

public sealed class OpenAICompatibleProviderAdapter
{
    private const int MaxRequestBodyBytes = 4 * 1024 * 1024;
    private const int MaxResponseBodyBytes = 4 * 1024 * 1024;
    private const int ResponseReadBufferSize = 8192;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Encoding StrictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly HttpClient _httpClient;
    private readonly OpenAICompatibleProviderOptions _options;
    private readonly Uri _chatCompletionsUri;

    public OpenAICompatibleProviderAdapter(
        HttpClient httpClient,
        OpenAICompatibleProviderOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _chatCompletionsUri = BuildChatCompletionsUri(_options.BaseUri);
    }

    public async Task<Result<OpenAICompatibleChatResponse>> CompleteChatAsync(
        OpenAICompatibleChatRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            _chatCompletionsUri);

        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        if (_options.ApiKey is not null)
        {
            try
            {
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        _options.ApiKey.Reveal());
            }
            catch (ArgumentException)
            {
                return Result<OpenAICompatibleChatResponse>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.invalid-credential",
                        ErrorCategory.Validation,
                        "The supplied provider credential cannot be used in an Authorization header."));
            }
            catch (FormatException)
            {
                return Result<OpenAICompatibleChatResponse>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.invalid-credential",
                        ErrorCategory.Validation,
                        "The supplied provider credential cannot be used in an Authorization header."));
            }
        }

        var payload = BuildPayload(request);
        Stream requestBody;

        try
        {
            requestBody = SerializeRequestBody(payload);
        }
        catch (RequestBodyTooLargeException)
        {
            return Result<OpenAICompatibleChatResponse>.Failure(
                new Error(
                    "hive.provider.openai-compatible.request-too-large",
                    ErrorCategory.Validation,
                    "The provider request exceeds the 4 MiB request-body limit."));
        }

        httpRequest.Content = new StreamContent(requestBody);
        httpRequest.Content.Headers.ContentType =
            new MediaTypeHeaderValue("application/json")
            {
                CharSet = StrictUtf8.WebName
            };

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Result<OpenAICompatibleChatResponse>.Failure(
                new Error(
                    "hive.provider.openai-compatible.timeout",
                    ErrorCategory.Timeout,
                    "The provider request timed out."));
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException(
                "The provider request was cancelled by the caller.",
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result<OpenAICompatibleChatResponse>.Failure(
                new Error(
                    "hive.provider.openai-compatible.transport-failed",
                    ErrorCategory.External,
                    "The provider request failed at the transport boundary."));
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                return Result<OpenAICompatibleChatResponse>.Failure(
                    MapHttpFailure(response.StatusCode));

            try
            {
                var responseBody = await ReadResponseBodyAsync(
                        response.Content,
                        timeoutCts.Token)
                    .ConfigureAwait(false);

                if (responseBody.IsFailure)
                    return Result<OpenAICompatibleChatResponse>.Failure(
                        responseBody.Error!);

                return ParseResponse(
                    responseBody.Value!,
                    request.StructuredOutput is not null,
                    request.Model);
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return Result<OpenAICompatibleChatResponse>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.timeout",
                        ErrorCategory.Timeout,
                        "The provider response timed out."));
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException(
                    "The provider response read was cancelled by the caller.",
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return Result<OpenAICompatibleChatResponse>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.transport-failed",
                        ErrorCategory.External,
                        "The provider response could not be read."));
            }
            catch (IOException)
            {
                return Result<OpenAICompatibleChatResponse>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.transport-failed",
                        ErrorCategory.External,
                        "The provider response could not be read."));
            }
            catch (DecoderFallbackException)
            {
                return SerializationFailure();
            }
        }
    }

    public async Task<Result<OpenAICompatibleModelCatalog>> ListModelsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var timeoutCts =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.Timeout);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            BuildModelsUri(_options.BaseUri));

        httpRequest.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        if (_options.ApiKey is not null)
        {
            try
            {
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        _options.ApiKey.Reveal());
            }
            catch (ArgumentException)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.invalid-credential",
                        ErrorCategory.Validation,
                        "The supplied provider credential cannot be used in an Authorization header."));
            }
            catch (FormatException)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.invalid-credential",
                        ErrorCategory.Validation,
                        "The supplied provider credential cannot be used in an Authorization header."));
            }
        }

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                    httpRequest,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return Result<OpenAICompatibleModelCatalog>.Failure(
                new Error(
                    "hive.provider.openai-compatible.timeout",
                    ErrorCategory.Timeout,
                    "The provider model-discovery request timed out."));
        }
        catch (OperationCanceledException)
        {
            throw new OperationCanceledException(
                "The provider model-discovery request was cancelled by the caller.",
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return Result<OpenAICompatibleModelCatalog>.Failure(
                new Error(
                    "hive.provider.openai-compatible.transport-failed",
                    ErrorCategory.External,
                    "The provider model-discovery request failed at the transport boundary."));
        }

        using (response)
        {
            if (response.StatusCode is
                HttpStatusCode.NotFound or
                HttpStatusCode.MethodNotAllowed or
                HttpStatusCode.NotImplemented)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    Error.Unsupported(
                        "hive.provider.openai-compatible.model-enumeration-unsupported",
                        "The provider does not expose an OpenAI-compatible model enumeration endpoint."));
            }

            if (!response.IsSuccessStatusCode)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    MapHttpFailure(response.StatusCode));
            }

            try
            {
                var responseBody = await ReadResponseBodyAsync(
                        response.Content,
                        timeoutCts.Token)
                    .ConfigureAwait(false);

                if (responseBody.IsFailure)
                {
                    return Result<OpenAICompatibleModelCatalog>.Failure(
                        responseBody.Error!);
                }

                return ParseModelCatalog(
                    responseBody.Value!,
                    TryGetRateLimitRemaining(response));
            }
            catch (OperationCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.timeout",
                        ErrorCategory.Timeout,
                        "The provider model-discovery response timed out."));
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException(
                    "The provider model-discovery response read was cancelled by the caller.",
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.transport-failed",
                        ErrorCategory.External,
                        "The provider model-discovery response could not be read."));
            }
            catch (IOException)
            {
                return Result<OpenAICompatibleModelCatalog>.Failure(
                    new Error(
                        "hive.provider.openai-compatible.transport-failed",
                        ErrorCategory.External,
                        "The provider model-discovery response could not be read."));
            }
            catch (DecoderFallbackException)
            {
                return ModelSerializationFailure();
            }
        }
    }

    private static async Task<Result<string>> ReadResponseBodyAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        var contentLength = content.Headers.ContentLength;

        if (contentLength is > MaxResponseBodyBytes)
            return Result<string>.Failure(ResponseTooLargeFailure());

        await using var stream = await content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        using var buffer = new MemoryStream(
            contentLength is >= 0
                ? checked((int)contentLength.Value)
                : 0);

        var readBuffer = new byte[ResponseReadBufferSize];
        long totalBytes = 0;

        while (true)
        {
            var read = await stream
                .ReadAsync(
                    readBuffer.AsMemory(),
                    cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
                break;

            totalBytes += read;

            if (totalBytes > MaxResponseBodyBytes)
                return Result<string>.Failure(ResponseTooLargeFailure());

            await buffer
                .WriteAsync(
                    readBuffer.AsMemory(0, read),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return Result<string>.Success(
            StrictUtf8.GetString(
                buffer.GetBuffer(),
                0,
                checked((int)totalBytes)));
    }

    private static Error ResponseTooLargeFailure() =>
        new(
            "hive.provider.openai-compatible.response-too-large",
            ErrorCategory.Serialization,
            "The provider response exceeds the 4 MiB response-body limit.");

    private static Stream SerializeRequestBody(object payload)
    {
        var buffer = new MemoryStream(16 * 1024);

        try
        {
            var boundedStream = new BoundedWriteStream(
                buffer,
                MaxRequestBodyBytes);

            JsonSerializer.Serialize(
                boundedStream,
                payload,
                JsonOptions);

            buffer.Position = 0;
            return buffer;
        }
        catch
        {
            buffer.Dispose();
            throw;
        }
    }

    private static Uri BuildModelsUri(Uri baseUri)
    {
        var path = baseUri.GetLeftPart(UriPartial.Path);

        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";

        return new Uri(
            path + "models" + baseUri.Query + baseUri.Fragment,
            UriKind.Absolute);
    }

    private static Uri BuildChatCompletionsUri(Uri baseUri)
    {
        var path = baseUri.GetLeftPart(UriPartial.Path);

        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";

        return new Uri(
            path + "chat/completions" + baseUri.Query + baseUri.Fragment,
            UriKind.Absolute);
    }

    private static Result<OpenAICompatibleModelCatalog> ParseModelCatalog(
        string responseJson,
        int? rateLimitRemaining)
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
                            : ProviderAvailabilityStatus.Unknown;

                var health = ParseHealth(model);
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
                var outputModalities = ParseStringList(
                    model,
                    "output_modalities",
                    "outputModalities",
                    "modalities_output",
                    "supported_output_modalities");
                var thinking = ParseThinking(model);
                var limits = ParseLimits(model);
                var pricing = ParsePricing(model);
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
                        family,
                        modelType,
                        category,
                        version,
                        operationalState,
                        outputModalities,
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
    }

    private static IReadOnlyList<OpenAICompatibleCapabilityDescriptor> ParseCapabilities(
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

        return states
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
                new OpenAICompatibleCapabilityDescriptor(
                    pair.Key,
                    pair.Value))
            .ToArray();
    }

    private static void AddBooleanCapability(
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

    private static void AddCapabilityFromPresence(
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

    private static void MergeCapabilityState(
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

    private static CapabilityState ParseCapabilityState(
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

    private static string? NormalizeCapabilityKey(string value)
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

    private static IReadOnlyList<string> ParseStringList(
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

        return values
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    private static (IReadOnlyList<string> Options, string? Default) ParseThinking(
        JsonElement model)
    {
        var options = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? defaultValue = null;

        foreach (var propertyName in new[]
        {
            "thinking_options",
            "thinking_levels",
            "supported_thinking_levels",
            "supported_reasoning_efforts"
        })
        {
            AddStringValues(model, propertyName, options);
        }

        if (model.TryGetProperty("thinking", out var thinking) &&
            thinking.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "options", "levels", "supported_levels" })
                AddStringValues(thinking, propertyName, options);

            defaultValue =
                TryGetString(thinking, "default") ??
                TryGetString(thinking, "default_level");
        }

        defaultValue ??=
            TryGetString(model, "default_thinking_level") ??
            TryGetString(model, "default_reasoning_effort");

        var reasoningEffort = TryGetString(model, "reasoning_effort");
        if (!string.IsNullOrWhiteSpace(reasoningEffort))
            options.Add(reasoningEffort);

        return (
            options
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .Take(32)
                .ToArray(),
            string.IsNullOrWhiteSpace(defaultValue)
                ? null
                : defaultValue.Trim());
    }

    private static void AddStringValues(
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

    private static ProviderModelLimits? ParseLimits(JsonElement model)
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

        if (context is null && maxInput is null && maxOutput is null && additional.Count == 0)
            return null;

        return new ProviderModelLimits(
            context,
            maxInput,
            maxOutput,
            additional);
    }

    private static long? FirstNonNegativeInt64(
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

    private static ProviderModelPricing? ParsePricing(JsonElement model)
    {
        var prices = new Dictionary<string, ProviderModelPrice>(StringComparer.Ordinal);
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
                ? TryGetString(pricing, "currency")
                : null;

        if (pricing.ValueKind == JsonValueKind.Object)
        {
            if (pricing.TryGetProperty("free", out free) &&
                free.ValueKind == JsonValueKind.True)
            {
                explicitFree = true;
            }

            foreach (var property in pricing.EnumerateObject())
            {
                if (string.Equals(property.Name, "currency", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "unit", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "unit_quantity", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(property.Name, "free", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                AddPrice(
                    prices,
                    property.Name,
                    property.Value,
                    defaultCurrency);
            }
        }

        if (prices.Count == 0 && !explicitFree)
            return null;

        return new ProviderModelPricing(
            prices.Values
                .OrderBy(item => item.BillingUnit, StringComparer.Ordinal)
                .ToArray(),
            explicitFree);
    }

    private static void AddPrice(
        IDictionary<string, ProviderModelPrice> prices,
        string propertyName,
        JsonElement value,
        string? defaultCurrency)
    {
        decimal? price = null;
        string? currency = defaultCurrency;
        decimal? unitQuantity = null;
        var unit = NormalizePricingUnit(propertyName);

        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.String)
        {
            price = TryGetDecimal(value);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            price = TryGetDecimalProperty(value, "price", "amount", "value");
            currency = TryGetString(value, "currency") ?? defaultCurrency;
            unit = TryGetString(value, "unit") ?? unit;
            unitQuantity = TryGetDecimalProperty(
                value,
                "unit_quantity",
                "quantity",
                "units");
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

    private static decimal? TryGetDecimalProperty(
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

    private static decimal? TryGetDecimal(JsonElement value)
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

    private static string NormalizePricingUnit(string propertyName)
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

    private static IReadOnlyDictionary<string, JsonElement> ParseExtensionData(
        JsonElement model)
    {
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "id",
            "object",
            "owned_by",
            "created",
            "available",
            "health",
            "input_modalities",
            "inputModalities",
            "modalities_input",
            "supported_input_modalities",
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

    private static bool TrySanitizeExtensionValue(
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

    private static bool IsSensitivePropertyName(string name)
    {
        var normalized = name.Trim().ToLowerInvariant();

        return normalized.Contains("authorization", StringComparison.Ordinal) ||
               normalized.Contains("access_token", StringComparison.Ordinal) ||
               normalized.Contains("token", StringComparison.Ordinal) ||
               normalized.Contains("secret", StringComparison.Ordinal) ||
               normalized.Contains("password", StringComparison.Ordinal) ||
               normalized.Contains("credential", StringComparison.Ordinal) ||
               normalized.Contains("privatekey", StringComparison.Ordinal) ||
               normalized.Contains("private_key", StringComparison.Ordinal) ||
               normalized.Contains("clientsecret", StringComparison.Ordinal) ||
               normalized.Contains("client_secret", StringComparison.Ordinal) ||
               normalized.Contains("cookie", StringComparison.Ordinal) ||
               normalized.Contains("webhook", StringComparison.Ordinal);
    }

    private static bool ContainsCredentialBearingUri(string value)
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
                string.Equals(name, "apikey", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, "authorization", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static ProviderHealthStatus ParseHealth(JsonElement model)
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

    private static string? TryGetString(
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

    private static int? TryGetRateLimitRemaining(
        HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues(
                "x-ratelimit-remaining-requests",
                out var values))
        {
            return null;
        }

        var value = values.FirstOrDefault();

        return int.TryParse(
                value,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out var remaining) && remaining >= 0
            ? remaining
            : null;
    }

    private static object BuildPayload(OpenAICompatibleChatRequest request)
    {
        var messages = request.Messages.Select(message => new
        {
            role = ToWireRole(message.Role),
            content = message.Content
        }).ToArray();

        object? responseFormat = null;

        if (request.StructuredOutput is not null)
        {
            responseFormat = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = request.StructuredOutput.Name,
                    strict = request.StructuredOutput.Strict,
                    schema = request.StructuredOutput.Schema
                }
            };
        }

        return new
        {
            model = request.Model,
            messages,
            response_format = responseFormat
        };
    }

    private static string ToWireRole(OpenAICompatibleMessageRole role) =>
        role switch
        {
            OpenAICompatibleMessageRole.System => "system",
            OpenAICompatibleMessageRole.User => "user",
            OpenAICompatibleMessageRole.Assistant => "assistant",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
        };

    private static Result<OpenAICompatibleChatResponse> ParseResponse(
        string responseJson,
        bool structuredOutputRequested,
        string requestedModel)
    {
        try
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                return SerializationFailure();
            }

            var choice = choices[0];

            if (choice.ValueKind != JsonValueKind.Object ||
                !choice.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return SerializationFailure();
            }

            var text = content.GetString();

            if (string.IsNullOrWhiteSpace(text))
                return SerializationFailure();

            var responseModel = root.TryGetProperty("model", out var modelElement) &&
                                modelElement.ValueKind == JsonValueKind.String
                ? modelElement.GetString()
                : null;

            var responseId = root.TryGetProperty("id", out var idElement) &&
                             idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString()
                : null;

            var model = string.IsNullOrWhiteSpace(responseModel)
                ? requestedModel
                : responseModel.Trim();

            var id = string.IsNullOrWhiteSpace(responseId)
                ? null
                : responseId.Trim();

            JsonElement? structuredContent = null;

            if (structuredOutputRequested)
            {
                using var structuredDocument = JsonDocument.Parse(text);
                structuredContent = structuredDocument.RootElement.Clone();
            }

            return Result<OpenAICompatibleChatResponse>.Success(
                new OpenAICompatibleChatResponse(
                    id,
                    model,
                    text,
                    structuredContent));
        }
        catch (JsonException)
        {
            return SerializationFailure();
        }
        catch (ArgumentException)
        {
            return SerializationFailure();
        }
    }

    private sealed class BoundedWriteStream : Stream
    {
        private readonly Stream _inner;
        private readonly long _maxBytes;

        public BoundedWriteStream(Stream inner, long maxBytes)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));

            if (maxBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytes));

            _maxBytes = maxBytes;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(
            byte[] buffer,
            int offset,
            int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(
            long offset,
            SeekOrigin origin) =>
            _inner.Seek(offset, origin);

        public override void SetLength(long value)
        {
            if (value > _maxBytes)
                throw new RequestBodyTooLargeException();

            _inner.SetLength(value);
        }

        public override void Write(
            byte[] buffer,
            int offset,
            int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);

            if (count < 0 ||
                offset < 0 ||
                offset > buffer.Length - count)
            {
                throw new ArgumentOutOfRangeException();
            }

            EnsureWriteFits(count);
            _inner.Write(buffer, offset, count);
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            EnsureWriteFits(buffer.Length);
            _inner.Write(buffer);
        }

        public override void WriteByte(byte value)
        {
            EnsureWriteFits(1);
            _inner.WriteByte(value);
        }

        private void EnsureWriteFits(int count)
        {
            if (count > _maxBytes - _inner.Length)
                throw new RequestBodyTooLargeException();
        }

        protected override void Dispose(bool disposing)
        {
        }
    }

    private sealed class RequestBodyTooLargeException : Exception
    {
    }

    private static Result<OpenAICompatibleModelCatalog> ModelSerializationFailure() =>
        Result<OpenAICompatibleModelCatalog>.Failure(
            new Error(
                "hive.provider.openai-compatible.malformed-model-catalog",
                ErrorCategory.Serialization,
                "The provider returned a malformed or unsupported model catalog."));

    private static Result<OpenAICompatibleChatResponse> SerializationFailure() =>
        Result<OpenAICompatibleChatResponse>.Failure(
            new Error(
                "hive.provider.openai-compatible.malformed-response",
                ErrorCategory.Serialization,
                "The provider returned a malformed or unsupported response."));

    private static Error MapHttpFailure(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                new Error(
                    "hive.provider.openai-compatible.authentication-failed",
                    ErrorCategory.Unauthorized,
                    "The provider rejected the supplied credentials."),

            HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout =>
                new Error(
                    "hive.provider.openai-compatible.timeout",
                    ErrorCategory.Timeout,
                    "The provider reported a request timeout."),

            (HttpStatusCode)429 =>
                new Error(
                    "hive.provider.openai-compatible.rate-limit",
                    ErrorCategory.External,
                    "The provider rate-limited the request."),

            _ when (int)statusCode >= 400 =>
                new Error(
                    "hive.provider.openai-compatible.http-failed",
                    ErrorCategory.External,
                    $"The provider returned HTTP {(int)statusCode}."),

            _ =>
                new Error(
                    "hive.provider.openai-compatible.unexpected-status",
                    ErrorCategory.External,
                    $"The provider returned an unexpected HTTP status {(int)statusCode}.")
        };
    }
}