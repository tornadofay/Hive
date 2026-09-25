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
        _chatCompletionsUri = new Uri(_options.BaseUri, "chat/completions");
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
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _options.ApiKey.Reveal());
        }

        var payload = BuildPayload(request);
        var requestBody = JsonSerializer.SerializeToUtf8Bytes(
            payload,
            JsonOptions);

        if (requestBody.Length > MaxRequestBodyBytes)
        {
            return Result<OpenAICompatibleChatResponse>.Failure(
                new Error(
                    "hive.provider.openai-compatible.request-too-large",
                    ErrorCategory.Validation,
                    "The provider request exceeds the 4 MiB request-body limit."));
        }

        httpRequest.Content = new ByteArrayContent(requestBody);
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
                    request.StructuredOutput is not null);
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
        bool structuredOutputRequested)
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

            if (!choice.TryGetProperty("message", out var message) ||
                message.ValueKind != JsonValueKind.Object ||
                !message.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.String)
            {
                return SerializationFailure();
            }

            var text = content.GetString();

            if (string.IsNullOrWhiteSpace(text))
                return SerializationFailure();

            var model = root.TryGetProperty("model", out var modelElement) &&
                        modelElement.ValueKind == JsonValueKind.String
                ? modelElement.GetString()
                : null;

            var id = root.TryGetProperty("id", out var idElement) &&
                     idElement.ValueKind == JsonValueKind.String
                ? idElement.GetString()
                : null;

            JsonElement? structuredContent = null;

            if (structuredOutputRequested)
            {
                using var structuredDocument = JsonDocument.Parse(text);
                structuredContent = structuredDocument.RootElement.Clone();
            }

            return Result<OpenAICompatibleChatResponse>.Success(
                new OpenAICompatibleChatResponse(
                    id,
                    model ?? string.Empty,
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