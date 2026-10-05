using System.Diagnostics;
using System.Text.Json;
using Hive.Agents;
using Hive.Core;
using Hive.Persistence;
using Hive.Providers.OpenAICompatible;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Hive.Coordination;

public sealed class AgentExecutionService
{
    private readonly IEventPersistenceStore _eventStore;
    private readonly HttpClient _httpClient;
    private readonly TimeSpan _providerTimeout;
    private readonly IClock _clock;

    public AgentExecutionService(
        HiveEventPersistenceComposition persistence,
        HttpClient httpClient,
        TimeSpan? providerTimeout = null,
        IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        _eventStore = persistence.EventStore;
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _clock = clock ?? SystemClock.Instance;

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

    public async Task<Result<AgentExecutionResult>> ExecuteAsync(
        AgentExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = ValidateRequest(request);
        if (validation.IsFailure)
            return Result<AgentExecutionResult>.Failure(validation.Error!);

        var executionResult = request.Runtime.StartExecution();
        if (executionResult.IsFailure)
            return Result<AgentExecutionResult>.Failure(executionResult.Error!);

        var execution = executionResult.Value!;
        var correlationId = request.CorrelationId ?? CorrelationId.New();
        var stream = new ResourceReference(
            ResourceKind.Execution,
            execution.Id.Value);

        var startedEnvelope = CreateLifecycleEvent(
            "agent.execution.started",
            correlationId,
            null,
            new
            {
                executionId = execution.Id.Value,
                agentId = execution.AgentId.Value,
                runtimeId = execution.RuntimeId.Value,
                targetId = request.Target.Id.Value,
                generation = execution.Generation.ToString(),
                model = request.Target.Model ?? request.Target.Deployment
            });

        Result<EventAppendResult> started;

        try
        {
            started = await _eventStore
                .AppendAsync(
                    new EventAppendRequest(
                        stream,
                        null,
                        startedEnvelope),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var cancelled = execution.Cancel();

            if (cancelled.IsSuccess)
            {
                await TryPersistInitialTerminalAsync(
                    request,
                    cancelled.Value!,
                    stream,
                    correlationId,
                    startedEnvelope,
                    "agent.execution.cancelled",
                    error: null)
                    .ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception)
        {
            var failed = execution.Fail();

            if (failed.IsSuccess)
            {
                await TryPersistInitialTerminalAsync(
                    request,
                    failed.Value!,
                    stream,
                    correlationId,
                    startedEnvelope,
                    "agent.execution.failed",
                    Error.Validation(
                        "hive.agent.execution.start-persistence-failed",
                        "The agent execution could not record its started lifecycle event."))
                    .ConfigureAwait(false);
            }

            return Result<AgentExecutionResult>.Failure(
                new Error(
                    "hive.agent.execution.start-persistence-failed",
                    ErrorCategory.External,
                    "The agent execution could not record its started lifecycle event."));
        }

        if (started.IsFailure)
        {
            var failed = execution.Fail();

            if (failed.IsSuccess)
            {
                await TryPersistInitialTerminalAsync(
                    request,
                    failed.Value!,
                    stream,
                    correlationId,
                    startedEnvelope,
                    "agent.execution.failed",
                    started.Error)
                    .ConfigureAwait(false);
            }

            return Result<AgentExecutionResult>.Failure(started.Error!);
        }

        var startedEvent = started.Value!.Event.Envelope;
        var pricingEvidence = ResolvePricingEvidence(request.PricingEvidence);

        try
        {
            var model = request.Target.Model ?? request.Target.Deployment;
            if (string.IsNullOrWhiteSpace(model))
            {
                return await PersistTerminalFailureAsync(
                        request,
                        execution,
                        stream,
                        correlationId,
                        startedEvent,
                        new Error(
                            "hive.agent.execution.model-required",
                            ErrorCategory.Validation,
                            "The selected execution target does not define a model or deployment."),
                        CancellationToken.None)
                    .ConfigureAwait(false);
            }

            var adapter = new OpenAICompatibleProviderAdapter(
                _httpClient,
                new OpenAICompatibleProviderOptions(
                    request.Target.Endpoint,
                    request.ApiKey,
                    _providerTimeout));

            using var chatClient = new OpenAICompatibleChatClient(
                adapter,
                model);

            var mafAgent = chatClient.AsAIAgent(
                name: request.Agent.Definition.Key,
                description: request.Agent.Definition.DisplayName);

            var response = await mafAgent
                .RunAsync(
                    request.UserMessage,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var responseMessage = response.Messages.LastOrDefault();
            var responseText = responseMessage?.Text;
            var providerResponseId = response.ResponseId;
            var usage = NormalizeUsage(response.Usage);

            if (string.IsNullOrWhiteSpace(responseText))
            {
                return await PersistTerminalFailureAsync(
                        request,
                        execution,
                        stream,
                        correlationId,
                        startedEvent,
                        new Error(
                            "hive.agent.execution.empty-response",
                            ErrorCategory.Serialization,
                            "The provider returned no usable agent response text."),
                        CancellationToken.None,
                        usage,
                        pricingEvidence)
                    .ConfigureAwait(false);
            }

            var completed = execution.Complete();

            if (completed.IsFailure)
                return Result<AgentExecutionResult>.Failure(completed.Error!);

            return await PersistTerminalSuccessAsync(
                    request,
                    completed.Value!,
                    stream,
                    correlationId,
                    startedEvent,
                    responseText,
                    providerResponseId,
                    usage,
                    pricingEvidence,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OpenAICompatibleProviderException exception)
        {
            return await PersistTerminalFailureAsync(
                    request,
                    execution,
                    stream,
                    correlationId,
                    startedEvent,
                    exception.Error,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            var cancelled = execution.Cancel();

            if (cancelled.IsFailure)
                return Result<AgentExecutionResult>.Failure(cancelled.Error!);

            return await PersistTerminalCancelledAsync(
                    request,
                    cancelled.Value!,
                    stream,
                    correlationId,
                    startedEvent,
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            return await PersistTerminalFailureAsync(
                    request,
                    execution,
                    stream,
                    correlationId,
                    startedEvent,
                    new Error(
                        "hive.agent.execution.failed",
                        ErrorCategory.Internal,
                        "Agent execution failed unexpectedly."),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static ExecutionTokenUsage NormalizeUsage(
        UsageDetails? usage)
    {
        if (usage is null)
            return ExecutionTokenUsage.Unknown;

        var additionalCounts =
            new Dictionary<string, long>(StringComparer.Ordinal);

        if (usage.AdditionalCounts is not null)
        {
            foreach (var pair in usage.AdditionalCounts)
            {
                if (additionalCounts.Count >=
                    ExecutionTokenUsage.MaxAdditionalCountEntries)
                {
                    break;
                }

                var key = pair.Key?.Trim();

                if (string.IsNullOrWhiteSpace(key) ||
                    key.Length >
                        ExecutionTokenUsage.MaxAdditionalCountKeyLength ||
                    pair.Value < 0 ||
                    additionalCounts.ContainsKey(key))
                {
                    continue;
                }

                additionalCounts[key] = pair.Value;
            }
        }

        var hasKnownUsage =
            usage.InputTokenCount is not null ||
            usage.OutputTokenCount is not null ||
            usage.TotalTokenCount is not null ||
            usage.CachedInputTokenCount is not null ||
            usage.ReasoningTokenCount is not null ||
            additionalCounts.Count != 0;

        if (!hasKnownUsage)
            return ExecutionTokenUsage.Unknown;

        return new ExecutionTokenUsage(
            TokenUsageEvidence.Actual,
            usage.InputTokenCount,
            usage.OutputTokenCount,
            usage.TotalTokenCount,
            usage.CachedInputTokenCount,
            usage.ReasoningTokenCount,
            additionalCounts);
    }

    private static object CreateUsagePayload(
        AgentExecutionRequest request,
        Execution execution,
        ExecutionTokenUsage usage,
        string? providerResponseId,
        ExecutionPricingEvidence? pricingEvidence)
    {
        var accessContext = request.AccessContext;

        return new
        {
            evidence = usage.Evidence.ToString(),
            inputTokenCount = usage.InputTokenCount,
            outputTokenCount = usage.OutputTokenCount,
            totalTokenCount = usage.TotalTokenCount,
            cachedInputTokenCount = usage.CachedInputTokenCount,
            reasoningTokenCount = usage.ReasoningTokenCount,
            additionalCounts = usage.AdditionalCounts,
            providerId = request.Target.ProviderId.Value,
            providerAccountId = request.Target.ProviderAccountId.Value,
            executionTargetId = request.Target.Id.Value,
            model = request.Target.Model,
            deployment = request.Target.Deployment,
            agentId = execution.AgentId.Value,
            runtimeId = execution.RuntimeId.Value,
            executionId = execution.Id.Value,
            deploymentId = accessContext.DeploymentId?.Value,
            tenantId = accessContext.TenantId?.Value,
            principalId = accessContext.PrincipalId?.Value,
            userId = accessContext.UserId?.Value,
            sessionId = accessContext.SessionId?.Value,
            workspaceId = accessContext.WorkspaceId?.Value,
            hiveId = accessContext.HiveId?.Value,
            providerResponseId,
            pricingEvidence = CreatePricingPayload(pricingEvidence)
        };
    }

    private static object? CreatePricingPayload(
        ExecutionPricingEvidence? pricingEvidence)
    {
        if (pricingEvidence is null)
            return null;

        return new
        {
            modelId = pricingEvidence.ModelId,
            observedAtUtc = pricingEvidence.ObservedAtUtc,
            staleAfterUtc = pricingEvidence.StaleAfterUtc,
            explicitFreeEvidence = pricingEvidence.Pricing.ExplicitFreeEvidence,
            prices = pricingEvidence.Pricing.Prices.Select(
                static price => new
                {
                    billingUnit = price.BillingUnit,
                    price = price.Price,
                    currency = price.Currency,
                    unitQuantity = price.UnitQuantity
                }),
            variants = pricingEvidence.Pricing.Variants.Select(
                static variant => new
                {
                    key = variant.Key,
                    isDefault = variant.IsDefault,
                    conditions = variant.Conditions,
                    prices = variant.Prices.Select(
                        static price => new
                        {
                            billingUnit = price.BillingUnit,
                            price = price.Price,
                            currency = price.Currency,
                            unitQuantity = price.UnitQuantity
                        })
                })
        };
    }

    private ExecutionPricingEvidence? ResolvePricingEvidence(
        ExecutionPricingEvidence? pricingEvidence) =>
        pricingEvidence is { } evidence && !evidence.IsStale(_clock.UtcNow)
            ? evidence
            : null;

    private static Result ValidateRequest(
        AgentExecutionRequest request)
    {
        if (request.Agent.Generation != AgentGeneration.Base)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.agent.execution.generation-not-supported",
                    "The first real execution slice supports only the Base Agent generation."));
        }

        if (request.Runtime.AgentId != request.Agent.Id)
        {
            return Result.Failure(
                Error.Validation(
                    "hive.agent.execution.runtime-mismatch",
                    "The runtime instance does not belong to the supplied Agent."));
        }

        if (request.Runtime.Generation != request.Agent.Generation)
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.agent.execution.generation-mismatch",
                    "The runtime generation does not match the supplied Agent generation."));
        }

        if (request.Target.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result.Failure(
                Error.Unsupported(
                    "hive.agent.execution.target-inactive",
                    "The selected execution target is not active."));
        }

        if (!request.Target.Resource.Scope.Matches(request.AccessContext))
        {
            return Result.Failure(
                Error.Validation(
                    "hive.agent.execution.target-context-mismatch",
                    "The selected execution target scope does not match the supplied execution context."));
        }

        return Result.Success();
    }

    private EventEnvelope CreateLifecycleEvent(
        string eventType,
        CorrelationId correlationId,
        CausationId? causationId,
        object payload,
        int payloadSchemaVersion = 1) =>
        CreateLifecycleEvent(
            EventId.New(),
            eventType,
            correlationId,
            causationId,
            payload,
            payloadSchemaVersion);

    private EventEnvelope CreateLifecycleEvent(
        EventId eventId,
        string eventType,
        CorrelationId correlationId,
        CausationId? causationId,
        object payload,
        int payloadSchemaVersion = 1) =>
        new JsonEventSerializer().CreateEnvelope(
            eventId,
            _clock.UtcNow,
            new EventType(eventType),
            new EventPayloadVersion(payloadSchemaVersion),
            correlationId,
            causationId,
            JsonSerializer.SerializeToElement(payload));

    private async Task<Result> TryPersistInitialTerminalAsync(
        AgentExecutionRequest request,
        Execution execution,
        ResourceReference stream,
        CorrelationId correlationId,
        EventEnvelope startedEvent,
        string eventType,
        Error? error)
    {
        var terminalEventId = EventId.New();
        Error? lastError = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            Result<IReadOnlyList<PersistedEvent>> events;

            try
            {
                events = await _eventStore
                    .ReadEventsAsync(
                        stream,
                        cancellationToken: CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                lastError = new Error(
                    "hive.agent.execution.initial-terminal-reconciliation-failed",
                    ErrorCategory.External,
                    "The agent execution could not reconcile its initial lifecycle persistence.");

                Trace.WriteLine(
                    $"Hive initial terminal reconciliation read failed: {lastError.Code} [{lastError.Category}] {exception.GetType().FullName}");
                continue;
            }

            if (events.IsFailure)
            {
                lastError = new Error(
                    "hive.agent.execution.initial-terminal-reconciliation-failed",
                    ErrorCategory.External,
                    "The agent execution could not reconcile its initial lifecycle persistence.");

                Trace.WriteLine(
                    $"Hive initial terminal reconciliation read failed: {events.Error?.Code} [{events.Error?.Category}]");
                continue;
            }

            var persistedEvents = events.Value!;
            var existingTerminal = persistedEvents.FirstOrDefault(
                eventItem => eventItem.Envelope.EventId == terminalEventId);

            if (existingTerminal is not null)
                return Result.Success();

            var currentVersion = persistedEvents
                .OrderByDescending(static eventItem => eventItem.StreamVersion.Value)
                .Select(static eventItem => (ResourceVersion?)eventItem.StreamVersion)
                .FirstOrDefault();

            var startedPersisted = persistedEvents.Any(
                eventItem => eventItem.Envelope.EventId == startedEvent.EventId);

            var terminalEnvelope = CreateLifecycleEvent(
                terminalEventId,
                eventType,
                correlationId,
                startedPersisted
                    ? new CausationId(startedEvent.EventId.Value)
                    : null,
                new
                {
                    executionId = execution.Id.Value,
                    agentId = execution.AgentId.Value,
                    runtimeId = execution.RuntimeId.Value,
                    targetId = request.Target.Id.Value,
                    status = execution.Status.ToString(),
                    errorCode = error?.Code,
                    errorCategory = error?.Category.ToString()
                });

            try
            {
                var append = await _eventStore
                    .AppendAsync(
                        new EventAppendRequest(
                            stream,
                            currentVersion,
                            terminalEnvelope),
                        CancellationToken.None)
                    .ConfigureAwait(false);

                if (append.IsSuccess)
                    return Result.Success();

                lastError = new Error(
                    "hive.agent.execution.initial-terminal-persistence-failed",
                    ErrorCategory.External,
                    "The initial terminal execution event could not be persisted.");

                Trace.WriteLine(
                    $"Hive initial terminal persistence append failed: {append.Error?.Code} [{append.Error?.Category}]");
            }
            catch (Exception exception)
            {
                lastError = new Error(
                    "hive.agent.execution.initial-terminal-persistence-failed",
                    ErrorCategory.External,
                    "The initial terminal execution event could not be persisted.");

                Trace.WriteLine(
                    $"Hive initial terminal persistence append failed: {lastError.Code} [{lastError.Category}] {exception.GetType().FullName}");
            }
        }

        return Result.Failure(
            lastError ??
            new Error(
                "hive.agent.execution.initial-terminal-recovery-failed",
                ErrorCategory.External,
                "The agent execution could not establish a terminal lifecycle outcome after initial persistence failed."));
    }

    private async Task<Result<AgentExecutionResult>> PersistTerminalSuccessAsync(
        AgentExecutionRequest request,
        Execution execution,
        ResourceReference stream,
        CorrelationId correlationId,
        EventEnvelope startedEvent,
        string responseText,
        string? providerResponseId,
        ExecutionTokenUsage usage,
        ExecutionPricingEvidence? pricingEvidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usage);

        var envelope = CreateLifecycleEvent(
            "agent.execution.succeeded",
            correlationId,
            new CausationId(startedEvent.EventId.Value),
            new
            {
                executionId = execution.Id.Value,
                agentId = execution.AgentId.Value,
                runtimeId = execution.RuntimeId.Value,
                targetId = request.Target.Id.Value,
                status = execution.Status.ToString(),
                responseLength = responseText.Length,
                providerResponseId,
                usage = CreateUsagePayload(
                    request,
                    execution,
                    usage,
                    providerResponseId,
                    pricingEvidence)
            },
            payloadSchemaVersion: 2);

        return await PersistTerminalAsync(
                request,
                execution,
                stream,
                startedEvent,
                envelope,
                responseText,
                providerResponseId,
                usage,
                pricingEvidence,
                correlationId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<AgentExecutionResult>> PersistTerminalFailureAsync(
        AgentExecutionRequest request,
        Execution execution,
        ResourceReference stream,
        CorrelationId correlationId,
        EventEnvelope startedEvent,
        Error error,
        CancellationToken cancellationToken,
        ExecutionTokenUsage? usage = null,
        ExecutionPricingEvidence? pricingEvidence = null)
    {
        var failed = execution.Fail();

        if (failed.IsFailure)
            return Result<AgentExecutionResult>.Failure(failed.Error!);

        object payload;
        var payloadSchemaVersion = 1;

        if (usage is null)
        {
            payload = new
            {
                executionId = execution.Id.Value,
                agentId = execution.AgentId.Value,
                runtimeId = execution.RuntimeId.Value,
                targetId = request.Target.Id.Value,
                status = failed.Value!.Status.ToString(),
                errorCode = error.Code,
                errorCategory = error.Category.ToString()
            };
        }
        else
        {
            payload = new
            {
                executionId = execution.Id.Value,
                agentId = execution.AgentId.Value,
                runtimeId = execution.RuntimeId.Value,
                targetId = request.Target.Id.Value,
                status = failed.Value!.Status.ToString(),
                errorCode = error.Code,
                errorCategory = error.Category.ToString(),
                usage = CreateUsagePayload(
                    request,
                    execution,
                    usage,
                    providerResponseId: null,
                    pricingEvidence: pricingEvidence)
            };

            payloadSchemaVersion = 2;
        }

        var envelope = CreateLifecycleEvent(
            "agent.execution.failed",
            correlationId,
            new CausationId(startedEvent.EventId.Value),
            payload,
            payloadSchemaVersion);

        var persisted = await PersistTerminalEventAsync(
            new EventAppendRequest(
                stream,
                ResourceVersion.Initial,
                envelope));

        if (persisted.IsFailure)
            return Result<AgentExecutionResult>.Failure(persisted.Error!);

        return Result<AgentExecutionResult>.Failure(error);
    }

    private async Task<Result<AgentExecutionResult>> PersistTerminalCancelledAsync(
        AgentExecutionRequest request,
        Execution execution,
        ResourceReference stream,
        CorrelationId correlationId,
        EventEnvelope startedEvent,
        CancellationToken cancellationToken)
    {
        var envelope = CreateLifecycleEvent(
            "agent.execution.cancelled",
            correlationId,
            new CausationId(startedEvent.EventId.Value),
            new
            {
                executionId = execution.Id.Value,
                agentId = execution.AgentId.Value,
                runtimeId = execution.RuntimeId.Value,
                targetId = request.Target.Id.Value,
                status = execution.Status.ToString()
            });

        var persisted = await PersistTerminalEventAsync(
            new EventAppendRequest(
                stream,
                ResourceVersion.Initial,
                envelope));

        if (persisted.IsFailure)
            return Result<AgentExecutionResult>.Failure(persisted.Error!);

        return Result<AgentExecutionResult>.Failure(
            new Error(
                "hive.agent.execution.cancelled",
                ErrorCategory.Cancelled,
                "Agent execution was cancelled."));
    }

    private async Task<Result<AgentExecutionResult>> PersistTerminalAsync(
        AgentExecutionRequest request,
        Execution execution,
        ResourceReference stream,
        EventEnvelope startedEvent,
        EventEnvelope terminalEvent,
        string responseText,
        string? providerResponseId,
        ExecutionTokenUsage usage,
        ExecutionPricingEvidence? pricingEvidence,
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var persisted = await PersistTerminalEventAsync(
            new EventAppendRequest(
                stream,
                ResourceVersion.Initial,
                terminalEvent));

        if (persisted.IsFailure)
            return Result<AgentExecutionResult>.Failure(persisted.Error!);

        return Result<AgentExecutionResult>.Success(
            new AgentExecutionResult(
                execution,
                responseText,
                request.Target.Id,
                correlationId,
                startedEvent.EventId,
                terminalEvent.EventId,
                providerResponseId,
                usage,
                pricingEvidence));
    }

    private async Task<Result<EventAppendResult>> PersistTerminalEventAsync(
        EventAppendRequest request)
    {
        Error? lastError = null;

        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                var result = await _eventStore
                    .AppendAsync(
                        request,
                        CancellationToken.None)
                    .ConfigureAwait(false);

                if (result.IsSuccess)
                    return result;

                lastError = new Error(
                    "hive.agent.execution.terminal-persistence-failed",
                    ErrorCategory.External,
                    "The terminal execution event could not be persisted.");

                var reconciled = await ReconcileTerminalEventAsync(request)
                    .ConfigureAwait(false);

                if (reconciled.IsSuccess)
                    return reconciled;
            }
            catch (Exception)
            {
                lastError = new Error(
                    "hive.agent.execution.terminal-persistence-failed",
                    ErrorCategory.External,
                    "The terminal execution event could not be persisted.");

                var reconciled = await ReconcileTerminalEventAsync(request)
                    .ConfigureAwait(false);

                if (reconciled.IsSuccess)
                    return reconciled;
            }
        }

        return Result<EventAppendResult>.Failure(
            lastError ??
            new Error(
                "hive.agent.execution.terminal-persistence-failed",
                ErrorCategory.External,
                "The terminal execution event could not be persisted."));
    }

    private async Task<Result<EventAppendResult>> ReconcileTerminalEventAsync(
        EventAppendRequest request)
    {
        try
        {
            var events = await _eventStore
                .ReadEventsAsync(
                    request.Stream,
                    request.ExpectedVersion ?? ResourceVersion.Initial,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (events.IsFailure)
                return Result<EventAppendResult>.Failure(events.Error!);

            var persisted = events.Value!
                .SingleOrDefault(
                    item => item.Envelope.EventId == request.Envelope.EventId);

            if (persisted is null)
                return Result<EventAppendResult>.Failure(
                    new Error(
                        "hive.agent.execution.terminal-not-reconciled",
                        ErrorCategory.NotFound,
                        "The terminal execution event was not found during persistence reconciliation."));

            return Result<EventAppendResult>.Success(
                new EventAppendResult(
                    persisted,
                    snapshot: null,
                    new EventOutboxEntry(
                        persisted.Stream,
                        persisted.StreamVersion,
                        persisted.Envelope)));
        }
        catch (Exception)
        {
            return Result<EventAppendResult>.Failure(
                new Error(
                    "hive.agent.execution.terminal-reconciliation-failed",
                    ErrorCategory.External,
                    "The terminal execution event could not be reconciled."));
        }
    }
}
