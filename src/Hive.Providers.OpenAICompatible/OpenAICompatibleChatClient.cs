using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace Hive.Providers.OpenAICompatible;

public sealed class OpenAICompatibleChatClient : IChatClient
{
    private readonly OpenAICompatibleProviderAdapter _adapter;
    private readonly string _defaultModel;

    public OpenAICompatibleChatClient(
        OpenAICompatibleProviderAdapter adapter,
        string defaultModel)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));

        if (string.IsNullOrWhiteSpace(defaultModel))
            throw new ArgumentException(
                "A default model is required.",
                nameof(defaultModel));

        _defaultModel = defaultModel.Trim();
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        if (options?.Tools is { Count: > 0 })
        {
            throw new OpenAICompatibleProviderException(
                new Hive.Core.Error(
                    "hive.provider.openai-compatible.tools-unsupported",
                    Hive.Core.ErrorCategory.Unsupported,
                    "Tool calling is not supported by this Phase 1.9 provider bridge."));
        }

        var model = string.IsNullOrWhiteSpace(options?.ModelId)
            ? _defaultModel
            : options.ModelId!.Trim();

        var request = new OpenAICompatibleChatRequest(
            model,
            ConvertMessages(messages));

        var result = await _adapter
            .CompleteChatAsync(request, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            throw new OpenAICompatibleProviderException(
                result.Error ?? new Hive.Core.Error(
                    "hive.provider.openai-compatible.unknown-failure",
                    Hive.Core.ErrorCategory.External,
                    "The OpenAI-compatible provider returned an unknown failure."));
        }

        var response = result.Value!;

        var chatResponse = new ChatResponse(
            new ChatMessage(
                ChatRole.Assistant,
                response.Content))
        {
            ModelId = response.Model,
            ResponseId = response.Id
        };

        return chatResponse;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GetResponseAsync(
                messages,
                options,
                cancellationToken)
            .ConfigureAwait(false);

        foreach (var update in response.ToChatResponseUpdates())
            yield return update;
    }

    public object? GetService(
        Type serviceType,
        object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);

        if (serviceKey is not null)
            return null;

        return serviceType.IsInstanceOfType(this)
            ? this
            : null;
    }

    public void Dispose()
    {
    }

    private static IReadOnlyList<OpenAICompatibleMessage> ConvertMessages(
        IEnumerable<ChatMessage> messages)
    {
        var converted = new List<OpenAICompatibleMessage>();

        foreach (var message in messages)
        {
            ArgumentNullException.ThrowIfNull(message);

            var role = message.Role switch
            {
                var value when value == ChatRole.System =>
                    OpenAICompatibleMessageRole.System,

                var value when value == ChatRole.User =>
                    OpenAICompatibleMessageRole.User,

                var value when value == ChatRole.Assistant =>
                    OpenAICompatibleMessageRole.Assistant,

                _ => throw new OpenAICompatibleProviderException(
                    new Hive.Core.Error(
                        "hive.provider.openai-compatible.message-role-unsupported",
                        Hive.Core.ErrorCategory.Unsupported,
                        $"Message role '{message.Role.Value}' is not supported by the OpenAI-compatible adapter."))
            };

            converted.Add(
                new OpenAICompatibleMessage(
                    role,
                    message.Text));
        }

        if (converted.Count == 0)
        {
            throw new OpenAICompatibleProviderException(
                new Hive.Core.Error(
                    "hive.provider.openai-compatible.empty-request",
                    Hive.Core.ErrorCategory.Validation,
                    "At least one chat message is required."));
        }

        return converted;
    }
}
