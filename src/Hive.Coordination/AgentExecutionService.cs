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

    public AgentExecutionService(
        IEventPersistenceStore eventStore,
        HttpClient httpClient,
        TimeSpan? providerTimeout = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
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

        var started = await _eventStore
            .AppendAsync(
                new EventAppendRequest(
                    stream,
                    null,
                    startedEnvelope),
                cancellationToken)
            .ConfigureAwait(false);

        if (started.IsFailure)
            return Result<AgentExecutionResult>.Failure(started.Error!);

        var startedEvent = started.Value!.Event.Envelope;

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
                        CancellationToken.None)
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
                    null,
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
        catch (Exception exception)
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
                        $"Agent execution failed unexpectedly: {exception.Message}"),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

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

    private static EventEnvelope CreateLifecycleEvent(
        string eventType,
        CorrelationId correlationId,
        CausationId? causationId,
        object payload) =>
        new JsonEventSerializer().CreateEnvelope(
            EventId.New(),
            DateTimeOffset.UtcNow,
            new EventType(eventType),
            new EventPayloadVersion(1),
            correlationId,
            causationId,
            JsonSerializer.SerializeToElement(payload));

    private async Task<Result<AgentExecutionResult>> PersistTerminalSuccessAsync(
        AgentExecutionRequest request,
        Execution execution,
        ResourceReference stream,
        CorrelationId correlationId,
        EventEnvelope startedEvent,
        string responseText,
        string? providerResponseId,
        CancellationToken cancellationToken)
    {
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
                providerResponseId
            });

        return await PersistTerminalAsync(
                request,
                execution,
                stream,
                startedEvent,
                envelope,
                responseText,
                providerResponseId,
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
        CancellationToken cancellationToken)
    {
        var failed = execution.Fail();

        if (failed.IsFailure)
            return Result<AgentExecutionResult>.Failure(failed.Error!);

        var envelope = CreateLifecycleEvent(
            "agent.execution.failed",
            correlationId,
            new CausationId(startedEvent.EventId.Value),
            new
            {
                executionId = execution.Id.Value,
                agentId = execution.AgentId.Value,
                runtimeId = execution.RuntimeId.Value,
                targetId = request.Target.Id.Value,
                status = failed.Value!.Status.ToString(),
                errorCode = error.Code,
                errorCategory = error.Category.ToString()
            });

        var persisted = await _eventStore
            .AppendAsync(
                new EventAppendRequest(
                    stream,
                    ResourceVersion.Initial,
                    envelope),
                cancellationToken)
            .ConfigureAwait(false);

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

        var persisted = await _eventStore
            .AppendAsync(
                new EventAppendRequest(
                    stream,
                    ResourceVersion.Initial,
                    envelope),
                cancellationToken)
            .ConfigureAwait(false);

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
        CorrelationId correlationId,
        CancellationToken cancellationToken)
    {
        var persisted = await _eventStore
            .AppendAsync(
                new EventAppendRequest(
                    stream,
                    ResourceVersion.Initial,
                    terminalEvent),
                cancellationToken)
            .ConfigureAwait(false);

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
                providerResponseId));
    }
}
