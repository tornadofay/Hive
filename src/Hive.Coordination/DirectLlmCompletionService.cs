using Hive.Core;
using Hive.Providers.OpenAICompatible;
using Microsoft.Extensions.AI;

namespace Hive.Coordination;

public enum DirectLlmChatRole
{
    User,
    Assistant
}

public sealed record DirectLlmChatMessage
{
    public DirectLlmChatMessage(DirectLlmChatRole role, string text)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Chat message text is required.", nameof(text));

        if (text.Length > 64 * 1024)
            throw new ArgumentException("Chat message text cannot exceed 64 KiB.", nameof(text));

        Role = role;
        Text = text;
    }

    public DirectLlmChatRole Role { get; }

    public string Text { get; }
}

public sealed record DirectLlmCompletion(
    string ResponseText,
    string? ProviderResponseId,
    string? ProviderReportedModelId);

/// <summary>
/// Executes a direct user-to-model chat completion without creating an Agent or Runtime.
/// Callers exposed to a host application must enter through Hive.Management so the
/// caller, selected target, credential, and durable conversation are authorized there.
/// </summary>
public sealed class DirectLlmCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _providerTimeout;

    public DirectLlmCompletionService(
        HttpClient httpClient,
        TimeSpan? providerTimeout = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        var effectiveTimeout = providerTimeout ?? TimeSpan.FromSeconds(30);
        if (effectiveTimeout <= TimeSpan.Zero ||
            effectiveTimeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(providerTimeout),
                effectiveTimeout,
                "Provider timeout must be greater than zero and no more than ten minutes.");
        }

        _providerTimeout = effectiveTimeout;
    }

    public async Task<Result<DirectLlmCompletion>> CompleteAsync(
        ExecutionTarget target,
        SecretMaterial? credential,
        IReadOnlyList<DirectLlmChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(messages);
        cancellationToken.ThrowIfCancellationRequested();

        if (messages.Count is 0 or > 64)
        {
            return Result<DirectLlmCompletion>.Failure(
                Error.Validation(
                    "hive.direct-llm.message-count-invalid",
                    "A direct LLM request must contain between 1 and 64 chat messages."));
        }

        var model = target.Model ?? target.Deployment;
        if (string.IsNullOrWhiteSpace(model))
        {
            return Result<DirectLlmCompletion>.Failure(
                Error.Validation(
                    "hive.direct-llm.model-required",
                    "The selected ExecutionTarget does not define a model or deployment."));
        }

        var chatMessages = new List<ChatMessage>(messages.Count);
        foreach (var message in messages)
        {
            if (message is null)
            {
                return Result<DirectLlmCompletion>.Failure(
                    Error.Validation(
                        "hive.direct-llm.message-invalid",
                        "The direct LLM history contains an invalid message."));
            }

            chatMessages.Add(
                new ChatMessage(
                    message.Role == DirectLlmChatRole.User
                        ? ChatRole.User
                        : ChatRole.Assistant,
                    message.Text));
        }

        try
        {
            var adapter = new OpenAICompatibleProviderAdapter(
                _httpClient,
                new OpenAICompatibleProviderOptions(
                    target.Endpoint,
                    credential,
                    _providerTimeout));

            using var chatClient = new OpenAICompatibleChatClient(adapter, model);
            var response = await chatClient.GetResponseAsync(
                    chatMessages,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var responseText = response.Messages.LastOrDefault()?.Text;
            if (string.IsNullOrWhiteSpace(responseText))
            {
                return Result<DirectLlmCompletion>.Failure(
                    Error.Validation(
                        "hive.direct-llm.empty-response",
                        "The provider returned no usable response text."));
            }

            return Result<DirectLlmCompletion>.Success(
                new DirectLlmCompletion(
                    responseText,
                    response.ResponseId,
                    response.ModelId));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (OpenAICompatibleProviderException exception)
        {
            return Result<DirectLlmCompletion>.Failure(exception.Error);
        }
        catch (Exception)
        {
            return Result<DirectLlmCompletion>.Failure(
                new Error(
                    "hive.direct-llm.execution-failed",
                    ErrorCategory.External,
                    "The direct LLM request could not be completed."));
        }
    }
}
