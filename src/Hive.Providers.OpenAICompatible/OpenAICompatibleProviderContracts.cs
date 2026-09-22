using System.Text.Json;
using Hive.Core;

namespace Hive.Providers.OpenAICompatible;

public enum OpenAICompatibleMessageRole
{
    System,
    User,
    Assistant
}

public sealed record OpenAICompatibleMessage
{
    public OpenAICompatibleMessage(OpenAICompatibleMessageRole role, string content)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), role, "Message role is invalid.");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Message content is required.", nameof(content));

        var normalized = content.Trim();

        if (normalized.Length > 64 * 1024)
            throw new ArgumentException("Message content cannot exceed 64 KiB.", nameof(content));

        Role = role;
        Content = normalized;
    }

    public OpenAICompatibleMessageRole Role { get; }

    public string Content { get; }
}

public sealed record OpenAICompatibleStructuredOutput
{
    public OpenAICompatibleStructuredOutput(
        string name,
        JsonElement schema,
        bool strict = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Structured output name is required.", nameof(name));

        var normalized = name.Trim();

        if (normalized.Length > 64)
            throw new ArgumentException("Structured output name cannot exceed 64 characters.", nameof(name));

        if (schema.ValueKind is not JsonValueKind.Object)
            throw new ArgumentException("Structured output schema must be a JSON object.", nameof(schema));

        Name = normalized;
        Schema = schema.Clone();
        Strict = strict;
    }

    public string Name { get; }

    public JsonElement Schema { get; }

    public bool Strict { get; }
}

public sealed class OpenAICompatibleChatRequest
{
    public OpenAICompatibleChatRequest(
        string model,
        IReadOnlyList<OpenAICompatibleMessage> messages,
        OpenAICompatibleStructuredOutput? structuredOutput = null)
    {
        if (string.IsNullOrWhiteSpace(model))
            throw new ArgumentException("Model is required.", nameof(model));

        var normalizedModel = model.Trim();

        if (normalizedModel.Length > 512)
            throw new ArgumentException("Model cannot exceed 512 characters.", nameof(model));

        ArgumentNullException.ThrowIfNull(messages);

        if (messages.Count == 0)
            throw new ArgumentException("At least one message is required.", nameof(messages));

        var normalizedMessages = messages.ToArray();

        foreach (var message in normalizedMessages)
            ArgumentNullException.ThrowIfNull(message);

        Model = normalizedModel;
        Messages = normalizedMessages;
        StructuredOutput = structuredOutput;
    }

    public string Model { get; }

    public IReadOnlyList<OpenAICompatibleMessage> Messages { get; }

    public OpenAICompatibleStructuredOutput? StructuredOutput { get; }
}

public sealed record OpenAICompatibleChatResponse(
    string? Id,
    string Model,
    string Content,
    JsonElement? StructuredContent);

public sealed class OpenAICompatibleProviderOptions
{
    public OpenAICompatibleProviderOptions(
        Uri baseUri,
        SecretMaterial? apiKey = null,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (!baseUri.IsAbsoluteUri ||
            (baseUri.Scheme != Uri.UriSchemeHttp &&
             baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Base URI must be an absolute HTTP or HTTPS URI.",
                nameof(baseUri));
        }

        if (!string.IsNullOrEmpty(baseUri.UserInfo))
            throw new ArgumentException(
                "Base URI must not embed credentials.",
                nameof(baseUri));

        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);

        if (effectiveTimeout <= TimeSpan.Zero ||
            effectiveTimeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                effectiveTimeout,
                "Provider timeout must be greater than zero and no more than ten minutes.");
        }

        BaseUri = EnsureTrailingSlash(baseUri);
        ApiKey = apiKey;
        Timeout = effectiveTimeout;
    }

    public Uri BaseUri { get; }

    public SecretMaterial? ApiKey { get; }

    public TimeSpan Timeout { get; }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        var text = uri.ToString();
        return text.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(text + "/", UriKind.Absolute);
    }
}