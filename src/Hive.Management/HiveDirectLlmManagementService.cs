using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hive.Coordination;
using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

internal sealed class HiveDirectLlmManagementService : HiveManagementServiceBase
{
    private const int MaxConversationPageSize = 100;
    private const int MaxPromptLength = 64 * 1024;
    private const int MaxStoredResponseLength = 128 * 1024;
    private const int MaxProviderContextMessages = 40;
    private static readonly TimeSpan StaleExecutionWindow = TimeSpan.FromMinutes(12);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IProviderResourceStore _providerResources;
    private readonly ISecretStore? _secrets;
    private readonly IEventPersistenceStore? _eventStore;
    private readonly DirectLlmCompletionService? _completionService;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<Guid, byte> _activeRequests = new();

    internal HiveDirectLlmManagementService(
        IProviderResourceStore providerResources,
        ISecretStore? secrets,
        IEventPersistenceStore? eventStore,
        DirectLlmCompletionService? completionService,
        IClock? clock = null)
    {
        _providerResources = providerResources
            ?? throw new ArgumentNullException(nameof(providerResources));
        _secrets = secrets;
        _eventStore = eventStore;
        _completionService = completionService;
        _clock = clock ?? SystemClock.Instance;
    }

    internal async Task<Result<DirectLlmConversationSummary>> CreateAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateConversationContext(accessContext);
        if (contextError is not null)
            return Result<DirectLlmConversationSummary>.Failure(contextError);

        if (!IsAvailable)
            return Result<DirectLlmConversationSummary>.Failure(UnavailableError());

        cancellationToken.ThrowIfCancellationRequested();

        var now = _clock.UtcNow;
        var id = ConversationId.New();
        var stream = GetStream(id);
        var state = new ConversationSnapshotState(
            id.Value,
            accessContext.DeploymentId!.Value.Value,
            accessContext.TenantId!.Value.Value,
            accessContext.PrincipalId!.Value.Value,
            accessContext.WorkspaceId?.Value,
            "New conversation",
            now,
            now,
            DirectLlmConversationStatus.Ready,
            0,
            null,
            null,
            null,
            null,
            null);

        var persisted = await AppendAsync(
            stream,
            expectedVersion: null,
            eventType: "workspace.direct-llm.conversation-created",
            correlationId: CorrelationId.New(),
            new ConversationEventPayload(
                Title: state.Title,
                OccurredAtUtc: now),
            state,
            cancellationToken).ConfigureAwait(false);

        if (persisted.IsFailure)
            return Result<DirectLlmConversationSummary>.Failure(persisted.Error!);

        return Result<DirectLlmConversationSummary>.Success(
            ToSummary(state, persisted.Value!.Event.StreamVersion));
    }

    internal async Task<Result<IReadOnlyList<DirectLlmConversationSummary>>> ListAsync(
        ResourceAccessContext accessContext,
        int pageSize = 30,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateConversationContext(accessContext);
        if (contextError is not null)
            return Result<IReadOnlyList<DirectLlmConversationSummary>>.Failure(contextError);

        if (!IsAvailable)
            return Result<IReadOnlyList<DirectLlmConversationSummary>>.Failure(UnavailableError());

        if (pageSize is < 1 or > MaxConversationPageSize)
        {
            return Result<IReadOnlyList<DirectLlmConversationSummary>>.Failure(
                Error.Validation(
                    "hive.direct-llm.conversation-page-size-invalid",
                    $"Conversation page size must be between 1 and {MaxConversationPageSize}."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var snapshots = await _eventStore!
            .ListSnapshotsAsync(ResourceKind.Conversation, cancellationToken)
            .ConfigureAwait(false);

        if (snapshots.IsFailure)
            return Result<IReadOnlyList<DirectLlmConversationSummary>>.Failure(snapshots.Error!);

        var visible = new List<(ConversationSnapshotState State, ResourceVersion Version)>();
        foreach (var snapshot in snapshots.Value!)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = DeserializeSnapshot(snapshot.State);
            if (state.IsFailure)
                return Result<IReadOnlyList<DirectLlmConversationSummary>>.Failure(state.Error!);

            if (OwnsConversation(state.Value!, accessContext))
                visible.Add((state.Value!, snapshot.Version));
        }

        var result = visible
            .OrderByDescending(static entry => entry.State.UpdatedAtUtc)
            .ThenBy(static entry => entry.State.ConversationId)
            .Take(pageSize)
            .Select(static entry => ToSummary(entry.State, entry.Version))
            .ToArray();

        return Result<IReadOnlyList<DirectLlmConversationSummary>>.Success(result);
    }

    internal async Task<Result<DirectLlmConversation>> GetAsync(
        ConversationId conversationId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateConversationContext(accessContext);
        if (contextError is not null)
            return Result<DirectLlmConversation>.Failure(contextError);

        if (conversationId == default)
        {
            return Result<DirectLlmConversation>.Failure(
                Error.Validation(
                    "hive.direct-llm.conversation-id-required",
                    "A conversation identity is required."));
        }

        if (!IsAvailable)
            return Result<DirectLlmConversation>.Failure(UnavailableError());

        var current = await GetOwnedSnapshotAsync(
            conversationId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (current.IsFailure)
            return Result<DirectLlmConversation>.Failure(current.Error!);

        var recovered = await RecoverStaleExecutionAsync(
            conversationId,
            current.Value!,
            cancellationToken).ConfigureAwait(false);

        if (recovered.IsFailure)
            return Result<DirectLlmConversation>.Failure(recovered.Error!);

        var snapshot = recovered.Value!;
        var events = await _eventStore!
            .ReadEventsAsync(GetStream(conversationId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (events.IsFailure)
            return Result<DirectLlmConversation>.Failure(events.Error!);

        var messages = new List<DirectLlmConversationMessage>();
        foreach (var persistedEvent in events.Value!)
        {
            if (!IsMessageEvent(persistedEvent.Envelope.EventType.Value))
                continue;

            var payload = DeserializeEvent(persistedEvent.Envelope.Payload);
            if (payload.IsFailure)
                return Result<DirectLlmConversation>.Failure(payload.Error!);

            if (payload.Value!.MessageId is not { } messageId ||
                payload.Value.Content is not { } text ||
                payload.Value.OccurredAtUtc is not { } occurredAtUtc)
            {
                return Result<DirectLlmConversation>.Failure(
                    CorruptConversationError());
            }

            var role = string.Equals(
                persistedEvent.Envelope.EventType.Value,
                "workspace.direct-llm.message-submitted",
                StringComparison.Ordinal)
                ? DirectLlmConversationMessageRole.User
                : DirectLlmConversationMessageRole.Assistant;

            messages.Add(
                new DirectLlmConversationMessage(
                    messageId,
                    role,
                    text,
                    occurredAtUtc,
                    ParseOptionalTarget(payload.Value.ExecutionTargetId),
                    payload.Value.ProviderReportedModelId));
        }

        return Result<DirectLlmConversation>.Success(
            new DirectLlmConversation(
                ToSummary(snapshot.State, snapshot.Version),
                messages));
    }

    internal async Task<Result<DirectLlmConversation>> SendMessageAsync(
        ConversationId conversationId,
        ExecutionTargetId executionTargetId,
        string userMessage,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateConversationContext(accessContext);
        if (contextError is not null)
            return Result<DirectLlmConversation>.Failure(contextError);

        if (conversationId == default)
        {
            return Result<DirectLlmConversation>.Failure(
                Error.Validation(
                    "hive.direct-llm.conversation-id-required",
                    "A conversation identity is required."));
        }

        if (executionTargetId == default)
        {
            return Result<DirectLlmConversation>.Failure(
                Error.Validation(
                    "hive.direct-llm.execution-target-required",
                    "Select an ExecutionTarget before sending a message."));
        }

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Result<DirectLlmConversation>.Failure(
                Error.Validation(
                    "hive.direct-llm.message-required",
                    "A message is required."));
        }

        var normalizedMessage = userMessage.Trim();
        if (normalizedMessage.Length > MaxPromptLength)
        {
            return Result<DirectLlmConversation>.Failure(
                Error.Validation(
                    "hive.direct-llm.message-too-large",
                    $"A message cannot exceed {MaxPromptLength / 1024} KiB."));
        }

        if (!IsAvailable)
            return Result<DirectLlmConversation>.Failure(UnavailableError());

        // Resolve all target and secret state before recording the user request.
        var targetContextResult = await ResolveTargetAsync(
            executionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (targetContextResult.IsFailure)
            return Result<DirectLlmConversation>.Failure(targetContextResult.Error!);

        var targetContext = targetContextResult.Value!;
        try
        {
            if (!_activeRequests.TryAdd(conversationId.Value, 0))
            {
                return Result<DirectLlmConversation>.Failure(
                    Error.Conflict(
                        "hive.direct-llm.conversation-request-in-progress",
                        "This conversation already has a request in progress."));
            }

            try
            {
                var current = await GetOwnedSnapshotAsync(
                    conversationId,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (current.IsFailure)
                    return Result<DirectLlmConversation>.Failure(current.Error!);

                var recovered = await RecoverStaleExecutionAsync(
                    conversationId,
                    current.Value!,
                    cancellationToken).ConfigureAwait(false);

                if (recovered.IsFailure)
                    return Result<DirectLlmConversation>.Failure(recovered.Error!);

                var currentSnapshot = recovered.Value!;
                if (currentSnapshot.State.Status == DirectLlmConversationStatus.Running)
                {
                    return Result<DirectLlmConversation>.Failure(
                        Error.Conflict(
                            "hive.direct-llm.conversation-request-in-progress",
                            "This conversation already has a request in progress."));
                }

                var previousMessages = await ReadMessagesAsync(
                    conversationId,
                    cancellationToken).ConfigureAwait(false);

                if (previousMessages.IsFailure)
                    return Result<DirectLlmConversation>.Failure(previousMessages.Error!);

                var now = _clock.UtcNow;
                var correlationId = CorrelationId.New();
                var messageId = Guid.NewGuid();
                var title = currentSnapshot.State.MessageCount == 0
                    ? CreateTitle(normalizedMessage)
                    : currentSnapshot.State.Title;

                var runningState = currentSnapshot.State with
                {
                    Title = title,
                    UpdatedAtUtc = now,
                    Status = DirectLlmConversationStatus.Running,
                    MessageCount = checked(currentSnapshot.State.MessageCount + 1),
                    LastExecutionTargetId = executionTargetId.ToString(),
                    ActiveCorrelationId = correlationId.ToString(),
                    LastErrorCode = null,
                    LastErrorMessage = null,
                    LastRequestStartedAtUtc = now,
                    LastProviderReportedModelId = null
                };

                var submitted = await AppendAsync(
                    GetStream(conversationId),
                    currentSnapshot.Version,
                    "workspace.direct-llm.message-submitted",
                    correlationId,
                    new ConversationEventPayload(
                        MessageId: messageId,
                        Content: normalizedMessage,
                        OccurredAtUtc: now,
                        ExecutionTargetId: executionTargetId.ToString(),
                        CorrelationId: correlationId.ToString()),
                    runningState,
                    cancellationToken).ConfigureAwait(false);

                if (submitted.IsFailure)
                    return Result<DirectLlmConversation>.Failure(submitted.Error!);

                var chatHistory = previousMessages.Value!
                    .TakeLast(MaxProviderContextMessages - 1)
                    .Select(static message => new DirectLlmChatMessage(
                        message.Role == DirectLlmConversationMessageRole.User
                            ? DirectLlmChatRole.User
                            : DirectLlmChatRole.Assistant,
                        message.Content))
                    .Append(new DirectLlmChatMessage(
                        DirectLlmChatRole.User,
                        normalizedMessage))
                    .ToArray();

                Result<DirectLlmCompletion> completion;
                try
                {
                    completion = await _completionService!
                        .CompleteAsync(
                            targetContext.Target,
                            targetContext.Credential,
                            chatHistory,
                            cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    await PersistTerminalFailureAsync(
                        conversationId,
                        submitted.Value!.Event.StreamVersion,
                        runningState,
                        correlationId,
                        DirectLlmConversationStatus.Cancelled,
                        new Error(
                            "hive.direct-llm.execution-cancelled",
                            ErrorCategory.Conflict,
                            "The direct LLM request was cancelled."),
                        CancellationToken.None).ConfigureAwait(false);
                    throw;
                }

                if (completion.IsFailure)
                {
                    var safeError = SanitizeExecutionError(completion.Error!);
                    var failed = await PersistTerminalFailureAsync(
                        conversationId,
                        submitted.Value!.Event.StreamVersion,
                        runningState,
                        correlationId,
                        DirectLlmConversationStatus.Failed,
                        safeError,
                        CancellationToken.None).ConfigureAwait(false);

                    return failed.IsFailure
                        ? Result<DirectLlmConversation>.Failure(failed.Error!)
                        : Result<DirectLlmConversation>.Failure(safeError);
                }

                var responseText = completion.Value!.ResponseText;
                if (responseText.Length > MaxStoredResponseLength)
                {
                    var oversized = Error.Validation(
                        "hive.direct-llm.response-too-large",
                        "The provider response exceeded the supported conversation response size.");

                    var failed = await PersistTerminalFailureAsync(
                        conversationId,
                        submitted.Value!.Event.StreamVersion,
                        runningState,
                        correlationId,
                        DirectLlmConversationStatus.Failed,
                        oversized,
                        CancellationToken.None).ConfigureAwait(false);

                    return failed.IsFailure
                        ? Result<DirectLlmConversation>.Failure(failed.Error!)
                        : Result<DirectLlmConversation>.Failure(oversized);
                }

                var completedAt = _clock.UtcNow;
                var completedState = runningState with
                {
                    UpdatedAtUtc = completedAt,
                    Status = DirectLlmConversationStatus.Completed,
                    MessageCount = checked(runningState.MessageCount + 1),
                    ActiveCorrelationId = null,
                    LastErrorCode = null,
                    LastErrorMessage = null,
                    LastRequestStartedAtUtc = null,
                    LastProviderReportedModelId = LimitText(
                        completion.Value.ProviderReportedModelId,
                        512)
                };

                var responsePersisted = await AppendAsync(
                    GetStream(conversationId),
                    submitted.Value!.Event.StreamVersion,
                    "workspace.direct-llm.response-received",
                    correlationId,
                    new ConversationEventPayload(
                        MessageId: Guid.NewGuid(),
                        Content: responseText,
                        OccurredAtUtc: completedAt,
                        ExecutionTargetId: executionTargetId.ToString(),
                        CorrelationId: correlationId.ToString(),
                        ProviderResponseId: LimitText(completion.Value.ProviderResponseId, 512),
                        ProviderReportedModelId: LimitText(completion.Value.ProviderReportedModelId, 512)),
                    completedState,
                    CancellationToken.None).ConfigureAwait(false);

                if (responsePersisted.IsFailure)
                    return Result<DirectLlmConversation>.Failure(responsePersisted.Error!);

                return await GetAsync(
                    conversationId,
                    accessContext,
                    CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _activeRequests.TryRemove(conversationId.Value, out _);
            }
        }
        finally
        {
            targetContext.Credential?.Dispose();
        }
    }

    private async Task<Result<ResolvedExecutionTarget>> ResolveTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var targetResult = await _providerResources.GetExecutionTargetAsync(
            executionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (targetResult.IsFailure)
            return Result<ResolvedExecutionTarget>.Failure(targetResult.Error!);

        var target = targetResult.Value!;
        if (target.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Unsupported(
                    "hive.direct-llm.execution-target-inactive",
                    "The selected ExecutionTarget is not active."));
        }

        var accountResult = await _providerResources.GetProviderAccountAsync(
            target.ProviderAccountId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (accountResult.IsFailure)
            return Result<ResolvedExecutionTarget>.Failure(accountResult.Error!);

        var account = accountResult.Value!;
        if (account.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Unsupported(
                    "hive.direct-llm.provider-account-inactive",
                    "The selected ExecutionTarget's ProviderAccount is not active."));
        }

        var providerResult = await _providerResources.GetProviderAsync(
            target.ProviderId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (providerResult.IsFailure)
            return Result<ResolvedExecutionTarget>.Failure(providerResult.Error!);

        var provider = providerResult.Value!;
        if (provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Unsupported(
                    "hive.direct-llm.provider-inactive",
                    "The selected ExecutionTarget's Provider is not active."));
        }

        if (account.ProviderId != provider.Id)
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Conflict(
                    "hive.direct-llm.provider-account-mismatch",
                    "The selected ExecutionTarget references a ProviderAccount owned by a different Provider."));
        }

        if (account.ProviderId != target.ProviderId)
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Conflict(
                    "hive.direct-llm.execution-target-provider-mismatch",
                    "The selected ExecutionTarget's Provider and ProviderAccount do not match."));
        }

        if (BuiltInProviderCatalog.Find(provider.Key)?.RequiresNativeIntegration == true)
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Unsupported(
                    "hive.direct-llm.native-provider-unsupported",
                    "The selected Provider requires a native integration that is not available in the direct LLM path."));
        }

        if (!string.Equals(
                provider.TransportKind,
                "openai-compatible",
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<ResolvedExecutionTarget>.Failure(
                Error.Unsupported(
                    "hive.direct-llm.provider-transport-unsupported",
                    "The selected Provider does not use the supported OpenAI-compatible chat transport."));
        }

        SecretMaterial? credential = null;
        if (account.CredentialSecret is not null)
        {
            if (_secrets is null)
            {
                return Result<ResolvedExecutionTarget>.Failure(
                    Error.Unsupported(
                        "hive.management.secret-store-unavailable",
                        "The Hive Secret Store is not configured."));
            }

            var secret = await _secrets.GetAsync(
                account.CredentialSecret.Value.Id,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (secret.IsFailure)
                return Result<ResolvedExecutionTarget>.Failure(secret.Error!);

            credential = secret.Value!.Material;
        }

        return Result<ResolvedExecutionTarget>.Success(
            new ResolvedExecutionTarget(target, credential));
    }

    private async Task<Result<SnapshotAndState>> GetOwnedSnapshotAsync(
        ConversationId conversationId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var snapshotResult = await _eventStore!
            .GetSnapshotAsync(GetStream(conversationId), cancellationToken)
            .ConfigureAwait(false);

        if (snapshotResult.IsFailure)
            return Result<SnapshotAndState>.Failure(snapshotResult.Error!);

        var snapshot = snapshotResult.Value;
        if (snapshot is null)
        {
            return Result<SnapshotAndState>.Failure(
                new Error(
                    "hive.direct-llm.conversation-not-found",
                    ErrorCategory.NotFound,
                    "The requested conversation was not found."));
        }

        var stateResult = DeserializeSnapshot(snapshot.State);
        if (stateResult.IsFailure)
            return Result<SnapshotAndState>.Failure(stateResult.Error!);

        var state = stateResult.Value!;
        if (!OwnsConversation(state, accessContext))
        {
            return Result<SnapshotAndState>.Failure(
                new Error(
                    "hive.direct-llm.conversation-not-found",
                    ErrorCategory.NotFound,
                    "The requested conversation was not found."));
        }

        return Result<SnapshotAndState>.Success(new SnapshotAndState(snapshot, state));
    }

    private async Task<Result<SnapshotAndState>> RecoverStaleExecutionAsync(
        ConversationId conversationId,
        SnapshotAndState current,
        CancellationToken cancellationToken)
    {
        if (current.State.Status != DirectLlmConversationStatus.Running ||
            _activeRequests.ContainsKey(conversationId.Value) ||
            current.State.LastRequestStartedAtUtc is null ||
            _clock.UtcNow - current.State.LastRequestStartedAtUtc.Value < StaleExecutionWindow)
        {
            return Result<SnapshotAndState>.Success(current);
        }

        var updatedAt = _clock.UtcNow;
        var interrupted = current.State with
        {
            UpdatedAtUtc = updatedAt,
            Status = DirectLlmConversationStatus.Interrupted,
            ActiveCorrelationId = null,
            LastErrorCode = "hive.direct-llm.execution-interrupted",
            LastErrorMessage = "The previous request did not complete before the application stopped.",
            LastRequestStartedAtUtc = null
        };

        var appended = await AppendAsync(
            GetStream(conversationId),
            current.Version,
            "workspace.direct-llm.execution-interrupted",
            CorrelationId.New(),
            new ConversationEventPayload(
                OccurredAtUtc: updatedAt,
                ErrorCode: interrupted.LastErrorCode,
                ErrorMessage: interrupted.LastErrorMessage),
            interrupted,
            cancellationToken).ConfigureAwait(false);

        if (appended.IsFailure)
            return Result<SnapshotAndState>.Failure(appended.Error!);

        return Result<SnapshotAndState>.Success(
            new SnapshotAndState(appended.Value!.Snapshot!, interrupted));
    }

    private async Task<Result<IReadOnlyList<DirectLlmConversationMessage>>> ReadMessagesAsync(
        ConversationId conversationId,
        CancellationToken cancellationToken)
    {
        var events = await _eventStore!
            .ReadEventsAsync(GetStream(conversationId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (events.IsFailure)
            return Result<IReadOnlyList<DirectLlmConversationMessage>>.Failure(events.Error!);

        var messages = new List<DirectLlmConversationMessage>();
        foreach (var persistedEvent in events.Value!)
        {
            if (!IsMessageEvent(persistedEvent.Envelope.EventType.Value))
                continue;

            var payload = DeserializeEvent(persistedEvent.Envelope.Payload);
            if (payload.IsFailure)
                return Result<IReadOnlyList<DirectLlmConversationMessage>>.Failure(payload.Error!);

            if (payload.Value!.MessageId is not { } messageId ||
                payload.Value.Content is not { } text ||
                payload.Value.OccurredAtUtc is not { } occurredAtUtc)
            {
                return Result<IReadOnlyList<DirectLlmConversationMessage>>.Failure(
                    CorruptConversationError());
            }

            var role = string.Equals(
                persistedEvent.Envelope.EventType.Value,
                "workspace.direct-llm.message-submitted",
                StringComparison.Ordinal)
                ? DirectLlmConversationMessageRole.User
                : DirectLlmConversationMessageRole.Assistant;

            messages.Add(
                new DirectLlmConversationMessage(
                    messageId,
                    role,
                    text,
                    occurredAtUtc,
                    ParseOptionalTarget(payload.Value.ExecutionTargetId),
                    payload.Value.ProviderReportedModelId));
        }

        return Result<IReadOnlyList<DirectLlmConversationMessage>>.Success(messages);
    }

    private async Task<Result<SnapshotAndState>> PersistTerminalFailureAsync(
        ConversationId conversationId,
        ResourceVersion expectedVersion,
        ConversationSnapshotState runningState,
        CorrelationId correlationId,
        DirectLlmConversationStatus terminalStatus,
        Error error,
        CancellationToken cancellationToken)
    {
        var safeError = SanitizeExecutionError(error);
        var updatedAt = _clock.UtcNow;
        var terminalState = runningState with
        {
            UpdatedAtUtc = updatedAt,
            Status = terminalStatus,
            ActiveCorrelationId = null,
            LastErrorCode = safeError.Code,
            LastErrorMessage = safeError.Message,
            LastRequestStartedAtUtc = null
        };

        var appended = await AppendAsync(
            GetStream(conversationId),
            expectedVersion,
            terminalStatus == DirectLlmConversationStatus.Cancelled
                ? "workspace.direct-llm.execution-cancelled"
                : "workspace.direct-llm.execution-failed",
            correlationId,
            new ConversationEventPayload(
                OccurredAtUtc: updatedAt,
                ErrorCode: safeError.Code,
                ErrorMessage: safeError.Message,
                ExecutionTargetId: terminalState.LastExecutionTargetId,
                CorrelationId: correlationId.ToString()),
            terminalState,
            cancellationToken).ConfigureAwait(false);

        if (appended.IsFailure)
            return Result<SnapshotAndState>.Failure(appended.Error!);

        return Result<SnapshotAndState>.Success(
            new SnapshotAndState(appended.Value!.Snapshot!, terminalState));
    }

    private async Task<Result<EventAppendResult>> AppendAsync(
        ResourceReference stream,
        ResourceVersion? expectedVersion,
        string eventType,
        CorrelationId correlationId,
        ConversationEventPayload payload,
        ConversationSnapshotState snapshotState,
        CancellationToken cancellationToken)
    {
        var nextVersion = expectedVersion is null
            ? ResourceVersion.Initial
            : expectedVersion.Value.Next();

        var envelope = EventEnvelope.Create(
            EventId.New(),
            _clock.UtcNow,
            new EventType(eventType),
            new EventPayloadVersion(1),
            correlationId,
            causationId: null,
            JsonSerializer.SerializeToElement(payload, JsonOptions));

        var snapshot = new EventSnapshot(
            stream,
            nextVersion,
            new EventPayloadVersion(1),
            JsonSerializer.SerializeToElement(snapshotState, JsonOptions));

        return await _eventStore!
            .AppendAsync(
                new EventAppendRequest(
                    stream,
                    expectedVersion,
                    envelope,
                    snapshot),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static Result<ConversationSnapshotState> DeserializeSnapshot(JsonElement json)
    {
        try
        {
            var state = json.Deserialize<ConversationSnapshotState>(JsonOptions);
            if (state is null ||
                state.ConversationId == Guid.Empty ||
                state.DeploymentId == Guid.Empty ||
                state.TenantId == Guid.Empty ||
                state.OwnerPrincipalId == Guid.Empty ||
                state.MessageCount < 0 ||
                !Enum.IsDefined(state.Status))
            {
                return Result<ConversationSnapshotState>.Failure(CorruptConversationError());
            }

            return Result<ConversationSnapshotState>.Success(state);
        }
        catch (JsonException)
        {
            return Result<ConversationSnapshotState>.Failure(CorruptConversationError());
        }
    }

    private static Result<ConversationEventPayload> DeserializeEvent(JsonElement json)
    {
        try
        {
            var payload = json.Deserialize<ConversationEventPayload>(JsonOptions);
            return payload is null
                ? Result<ConversationEventPayload>.Failure(CorruptConversationError())
                : Result<ConversationEventPayload>.Success(payload);
        }
        catch (JsonException)
        {
            return Result<ConversationEventPayload>.Failure(CorruptConversationError());
        }
    }

    private static bool OwnsConversation(
        ConversationSnapshotState state,
        ResourceAccessContext context) =>
        context.DeploymentId is { } deploymentId &&
        context.TenantId is { } tenantId &&
        context.PrincipalId is { } principalId &&
        state.DeploymentId == deploymentId.Value &&
        state.TenantId == tenantId.Value &&
        state.OwnerPrincipalId == principalId.Value;

    private static Error? ValidateConversationContext(ResourceAccessContext accessContext)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return contextError;

        if (accessContext.TenantId is null)
        {
            return Error.Validation(
                "hive.direct-llm.tenant-required",
                "A tenant identity is required to manage Workspace conversations.");
        }

        return null;
    }

    private static DirectLlmConversationSummary ToSummary(
        ConversationSnapshotState state,
        ResourceVersion version) =>
        new(
            new ConversationId(state.ConversationId),
            state.Title,
            state.CreatedAtUtc,
            state.UpdatedAtUtc,
            state.Status,
            state.MessageCount,
            ParseOptionalTarget(state.LastExecutionTargetId),
            ParseOptionalCorrelation(state.ActiveCorrelationId),
            state.LastErrorCode,
            state.LastErrorMessage,
            version);

    private static ExecutionTargetId? ParseOptionalTarget(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : ExecutionTargetId.Parse(value);

    private static CorrelationId? ParseOptionalCorrelation(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : CorrelationId.Parse(value);

    private static ResourceReference GetStream(ConversationId id) =>
        new(ResourceKind.Conversation, id.Value);

    private static bool IsMessageEvent(string eventType) =>
        string.Equals(eventType, "workspace.direct-llm.message-submitted", StringComparison.Ordinal) ||
        string.Equals(eventType, "workspace.direct-llm.response-received", StringComparison.Ordinal);

    private static string CreateTitle(string message)
    {
        var firstLine = message
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static line => line.Trim())
            .FirstOrDefault(static line => line.Length > 0);

        return LimitText(firstLine, 72) ?? "New conversation";
    }

    private static string? LimitText(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var normalized = value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..(maxLength - 1)] + "…";
    }

    private static Error SanitizeExecutionError(Error error) =>
        error.Category is ErrorCategory.External or ErrorCategory.Internal
            ? new Error(error.Code, error.Category, "The direct LLM request failed. See diagnostics for the operation error code.")
            : error;

    private static Error UnavailableError() =>
        Error.Unsupported(
            "hive.direct-llm.conversation-unavailable",
            "Direct LLM conversations are not configured in the current Management service graph.");

    private static Error CorruptConversationError() =>
        new(
            "hive.direct-llm.conversation-corrupt",
            ErrorCategory.Serialization,
            "The stored Workspace conversation could not be read because its durable state is invalid.");

    private bool IsAvailable =>
        _eventStore is not null && _completionService is not null;

    private sealed record ResolvedExecutionTarget(
        ExecutionTarget Target,
        SecretMaterial? Credential);

    private sealed record SnapshotAndState(
        EventSnapshot Snapshot,
        ConversationSnapshotState State)
    {
        public ResourceVersion Version => Snapshot.Version;
    }

    private sealed record ConversationSnapshotState(
        Guid ConversationId,
        Guid DeploymentId,
        Guid TenantId,
        Guid OwnerPrincipalId,
        Guid? WorkspaceId,
        string Title,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DirectLlmConversationStatus Status,
        int MessageCount,
        string? LastExecutionTargetId,
        string? ActiveCorrelationId,
        string? LastErrorCode,
        string? LastErrorMessage,
        string? LastProviderReportedModelId,
        DateTimeOffset? LastRequestStartedAtUtc);

    private sealed record ConversationEventPayload(
        Guid? MessageId = null,
        string? Content = null,
        DateTimeOffset? OccurredAtUtc = null,
        string? ExecutionTargetId = null,
        string? CorrelationId = null,
        string? ProviderResponseId = null,
        string? ProviderReportedModelId = null,
        string? ErrorCode = null,
        string? ErrorMessage = null,
        string? Title = null);
}
