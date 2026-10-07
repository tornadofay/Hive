using System.Data;
using System.Text.Json;
using Hive.Agents;
using Hive.Core;
using Microsoft.Data.Sqlite;
using SqlConnection = Microsoft.Data.Sqlite.SqliteConnection;
using SqlCommand = Microsoft.Data.Sqlite.SqliteCommand;
using SqlTransaction = Microsoft.Data.Sqlite.SqliteTransaction;
using SqlDataReader = Microsoft.Data.Sqlite.SqliteDataReader;
using SqlException = Microsoft.Data.Sqlite.SqliteException;
using SqlParameter = Microsoft.Data.Sqlite.SqliteParameter;
using SqlDbType = Hive.Persistence.EmbeddedSqliteDbType;

namespace Hive.Persistence;

internal sealed class EmbeddedAgentWorkStateStore :
    IObjectiveStore,
    IAgentMemoryStore,
    IQuestionTransport,
    IDelegationChannel
{
    private enum WorkStateKind
    {
        Objective = 1,
        Memory = 2,
        Question = 3,
        Delegation = 4
    }

    private const int PayloadSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly EmbeddedPersistenceDatabase _options;
    private readonly IClock _clock;
    private readonly EmbeddedEventPersistenceStore _eventStore;

    public EmbeddedAgentWorkStateStore(
        EmbeddedPersistenceDatabase options,
        IClock? clock = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _clock = clock ?? SystemClock.Instance;
        _eventStore = new EmbeddedEventPersistenceStore(
            _options,
            clock: _clock);
    }

    public Result<Objective> Create(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string title,
        ObjectiveUpdate state,
        DateTimeOffset createdAtUtc,
        WorkItemBinding? workItemBinding = null)
    {
        var result = Objective.Create(
            accessContext,
            agentId,
            runtimeId,
            title,
            state,
            createdAtUtc,
            workItemBinding);

        if (result.IsFailure)
            return result;

        var objective = result.Value!;
        var row = CreateResourceRow(
            WorkStateKind.Objective,
            objective.AgentId,
            objective.RuntimeId,
            objective.Resource.Owner,
            objective.Resource.Scope,
            objective.Resource.Version,
            objective.Resource.Provenance,
            objective.Resource.Lifecycle,
            objective.Id.Value,
            (int)objective.Status,
            null,
            null,
            Serialize(new ObjectiveDocument(
                objective.Title,
                objective.CompletionCriteria,
                objective.Priority,
                objective.DeadlineUtc,
                objective.Dependencies
                    .Select(static value => value.Value)
                    .ToArray(),
                ToBindingDocument(objective.WorkItemBinding))));

        var persisted = ExecuteTransaction(
            "objective-create",
            (connection, transaction) =>
            {
                InsertState(connection, transaction, row);

                return AppendEvent(
                    connection,
                    transaction,
                    row,
                    null,
                    "agent.objective.created");
            });

        return persisted.IsFailure
            ? Result<Objective>.Failure(persisted.Error!)
            : Result<Objective>.Success(objective);
    }

    public Result<Objective> Get(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<Objective>.Failure(validation.Error!);

        try
        {
            var row = LoadRow(
                WorkStateKind.Objective,
                objectiveId.Value);

            if (row is null)
            {
                return Result<Objective>.Failure(
                    NotFound(
                        "hive.agent.objective.not-found",
                        "The requested objective does not exist in this RuntimeInstance."));
            }

            var accessError = ValidateRowAccess(
                row,
                accessContext,
                agentId,
                runtimeId,
                "hive.agent.objective.runtime-mismatch",
                "The requested objective belongs to another runtime boundary.");

            return accessError is not null
                ? Result<Objective>.Failure(accessError)
                : Result<Objective>.Success(RestoreObjective(row));
        }
        catch (SqlException exception)
        {
            return Result<Objective>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.agent-work-state.get-objective-sql",
                    "The durable objective read failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<Objective>.Failure(
                HivePersistenceError.Internal(
                    "hive.persistence.agent-work-state.get-objective",
                    "The durable objective state could not be reconstructed.",
                    exception));
        }
    }

    public Result<Objective> Update(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        ObjectiveUpdate update,
        DateTimeOffset changedAtUtc) =>
        TransitionObjective(
            "objective-update",
            accessContext,
            agentId,
            runtimeId,
            objectiveId,
            "agent.objective.updated",
            changedAtUtc,
            objective => objective.Update(
                accessContext,
                update,
                changedAtUtc));

    public Result<Objective> BindWorkItem(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        WorkItemBinding binding,
        DateTimeOffset changedAtUtc) =>
        TransitionObjective(
            "objective-bind-work-item",
            accessContext,
            agentId,
            runtimeId,
            objectiveId,
            "agent.objective.work-item-bound",
            changedAtUtc,
            objective => objective.BindWorkItem(
                accessContext,
                binding,
                changedAtUtc));

    public Result<Objective> Complete(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        DateTimeOffset completedAtUtc) =>
        TransitionObjective(
            "objective-complete",
            accessContext,
            agentId,
            runtimeId,
            objectiveId,
            "agent.objective.completed",
            completedAtUtc,
            objective => objective.Complete(
                accessContext,
                completedAtUtc));

    public Result<Objective> Cancel(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        DateTimeOffset cancelledAtUtc) =>
        TransitionObjective(
            "objective-cancel",
            accessContext,
            agentId,
            runtimeId,
            objectiveId,
            "agent.objective.cancelled",
            cancelledAtUtc,
            objective => objective.Cancel(
                accessContext,
                cancelledAtUtc));

    public Result<AgentMemoryEntry> Store(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key,
        string content,
        DateTimeOffset storedAtUtc,
        WorkItemBinding? workItemBinding = null,
        MemoryEvidenceKind evidenceKind = MemoryEvidenceKind.Actual)
    {
        var result = AgentMemoryEntry.Create(
            accessContext,
            agentId,
            runtimeId,
            key,
            content,
            storedAtUtc,
            workItemBinding,
            evidenceKind);

        if (result.IsFailure)
            return result;

        var memory = result.Value!;
        var row = CreateResourceRow(
            WorkStateKind.Memory,
            memory.AgentId,
            memory.RuntimeId,
            memory.Resource.Owner,
            memory.Resource.Scope,
            memory.Resource.Version,
            memory.Resource.Provenance,
            memory.Resource.Lifecycle,
            memory.Id.Value,
            0,
            memory.Key,
            null,
            Serialize(new MemoryDocument(
                memory.Key,
                memory.Content,
                (int)memory.EvidenceKind,
                ToBindingDocument(workItemBinding))));

        var persisted = ExecuteTransaction(
            "memory-store",
            (connection, transaction) =>
            {
                InsertState(connection, transaction, row);

                return AppendEvent(
                    connection,
                    transaction,
                    row,
                    null,
                    "agent.memory.stored");
            });

        return persisted.IsFailure
            ? Result<AgentMemoryEntry>.Failure(persisted.Error!)
            : Result<AgentMemoryEntry>.Success(memory);
    }

    public Result<IReadOnlyList<AgentMemoryEntry>> Retrieve(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<IReadOnlyList<AgentMemoryEntry>>.Failure(
                validation.Error!);

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<IReadOnlyList<AgentMemoryEntry>>.Failure(
                Error.Validation(
                    "hive.agent.memory.key-required",
                    "Memory key is required."));
        }

        try
        {
            using var connection = OpenConnection();
            using var command = CreateCommand(
                connection,
                """
                SELECT
                    [WorkStateKind],
                    [StateId],
                    [AgentId],
                    [RuntimeId],
                    [OwnerPrincipalId],
                    [ScopeKind],
                    [ScopeIdentity],
                    [ResourceVersion],
                    [CreatedAtUtc],
                    [CorrelationId],
                    [CausationId],
                    [SourceKind],
                    [SourceIdentity],
                    [LifecycleStatus],
                    [LifecycleChangedAtUtc],
                    [StateStatus],
                    [StateKey],
                    [ExpiresAtUtc],
                    [StateJson],
                    [UpdatedAtUtc]
                FROM [HiveAgentWorkState]
                WHERE [WorkStateKind] = @Kind
                  AND [AgentId] = @AgentId
                  AND [RuntimeId] = @RuntimeId
                  AND [OwnerPrincipalId] = @OwnerPrincipalId
                  AND [StateKey] = @StateKey
                ORDER BY [CreatedAtUtc], [StateId];
                LIMIT 1;
                """);

            command.Parameters.Add(
                IntParameter("@Kind", (int)WorkStateKind.Memory));
            command.Parameters.Add(
                GuidParameter("@AgentId", agentId.Value));
            command.Parameters.Add(
                GuidParameter("@RuntimeId", runtimeId.Value));
            command.Parameters.Add(
                GuidParameter(
                    "@OwnerPrincipalId",
                    accessContext.PrincipalId!.Value.Value));
            command.Parameters.Add(
                new SqlParameter(
                    "@StateKey",
                    SqlDbType.NVarChar,
                    -1)
                {
                    Value = key.Trim()
                });

            using var reader = command.ExecuteReader();
            var entries = new List<AgentMemoryEntry>();

            while (reader.Read())
                entries.Add(RestoreMemory(ReadRow(reader)));

            return Result<IReadOnlyList<AgentMemoryEntry>>.Success(entries);
        }
        catch (SqlException exception)
        {
            return Result<IReadOnlyList<AgentMemoryEntry>>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.agent-work-state.retrieve-memory-sql",
                    "The durable memory retrieval failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<AgentMemoryEntry>>.Failure(
                HivePersistenceError.Internal(
                    "hive.persistence.agent-work-state.retrieve-memory",
                    "The durable memory records could not be reconstructed.",
                    exception));
        }
    }

    public Result<AgentMemoryEntry> Get(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        MemoryId memoryId)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<AgentMemoryEntry>.Failure(validation.Error!);

        try
        {
            var row = LoadRow(
                WorkStateKind.Memory,
                memoryId.Value);

            if (row is null)
            {
                return Result<AgentMemoryEntry>.Failure(
                    NotFound(
                        "hive.agent.memory.not-found",
                        "The requested memory entry does not exist in this RuntimeInstance."));
            }

            var accessError = ValidateRowAccess(
                row,
                accessContext,
                agentId,
                runtimeId,
                "hive.agent.memory.runtime-mismatch",
                "The requested memory entry belongs to another runtime boundary.");

            return accessError is not null
                ? Result<AgentMemoryEntry>.Failure(accessError)
                : Result<AgentMemoryEntry>.Success(RestoreMemory(row));
        }
        catch (SqlException exception)
        {
            return Result<AgentMemoryEntry>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.agent-work-state.get-memory-sql",
                    "The durable memory read failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<AgentMemoryEntry>.Failure(
                HivePersistenceError.Internal(
                    "hive.persistence.agent-work-state.get-memory",
                    "The durable memory record could not be reconstructed.",
                    exception));
        }
    }

    public Result<Question> Ask(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string prompt,
        TimeSpan timeout,
        DateTimeOffset askedAtUtc)
    {
        var question = Question.Create(
            accessContext,
            agentId,
            runtimeId,
            prompt,
            timeout,
            askedAtUtc,
            CorrelationId.New());

        var row = CreateQuestionRow(question);

        var persisted = ExecuteTransaction(
            "question-ask",
            (connection, transaction) =>
            {
                InsertState(connection, transaction, row);

                return AppendEvent(
                    connection,
                    transaction,
                    row,
                    null,
                    "agent.question.created");
            });

        return persisted.IsFailure
            ? Result<Question>.Failure(persisted.Error!)
            : Result<Question>.Success(question);
    }

    public async Task<Result<Question>> WaitAsync(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId,
        CancellationToken cancellationToken = default)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<Question>.Failure(validation.Error!);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Result<Question>.Failure(
                    Error.Cancelled(
                        "hive.agent.question.wait-cancelled",
                        "Waiting for the Question answer was cancelled."));
            }

            var current = GetQuestion(
                accessContext,
                agentId,
                runtimeId,
                questionId);

            if (current.IsFailure)
                return current;

            var question = current.Value!;
            if (question.Status != QuestionStatus.Waiting)
                return current;

            if (question.ExpiresAtUtc <= _clock.UtcNow)
            {
                ExpireDue();

                await Task.Yield();
                continue;
            }

            var remaining = question.ExpiresAtUtc - _clock.UtcNow;
            var delay = remaining > TimeSpan.FromMilliseconds(100)
                ? TimeSpan.FromMilliseconds(100)
                : remaining;

            try
            {
                await Task.Delay(
                    delay <= TimeSpan.Zero
                        ? TimeSpan.FromMilliseconds(10)
                        : delay,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Result<Question>.Failure(
                    Error.Cancelled(
                        "hive.agent.question.wait-cancelled",
                        "Waiting for the Question answer was cancelled."));
            }
        }
    }

    public Result<Question> Answer(
        ResourceAccessContext responderContext,
        QuestionId questionId,
        AgentId responderAgentId,
        RuntimeId responderRuntimeId,
        string answer,
        DateTimeOffset answeredAtUtc)
    {
        return TransitionQuestion(
            "question-answer",
            responderContext,
            questionId,
            responderAgentId,
            responderRuntimeId,
            "agent.question.answered",
            answeredAtUtc,
            question => question.ApplyAnswer(
                responderAgentId,
                responderRuntimeId,
                answer,
                answeredAtUtc));
    }

    public Result<Question> Cancel(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId,
        DateTimeOffset cancelledAtUtc)
    {
        return TransitionQuestion(
            "question-cancel",
            accessContext,
            questionId,
            agentId,
            runtimeId,
            "agent.question.cancelled",
            cancelledAtUtc,
            question => question.Cancel(cancelledAtUtc));
    }

    public int ExpireDue()
    {
        List<Guid> ids;

        try
        {
            using var connection = OpenConnection();
            using var command = CreateCommand(
                connection,
                """
                SELECT [StateId]
                FROM [HiveAgentWorkState]
                WHERE [WorkStateKind] = @Kind
                  AND [StateStatus] = @Waiting
                  AND [ExpiresAtUtc] <= @NowUtc
                ORDER BY [ExpiresAtUtc], [StateId];
                LIMIT 1;
                """);

            command.Parameters.Add(
                IntParameter("@Kind", (int)WorkStateKind.Question));
            command.Parameters.Add(
                IntParameter("@Waiting", (int)QuestionStatus.Waiting));
            command.Parameters.Add(
                DateTimeParameter("@NowUtc", _clock.UtcNow));

            using var reader = command.ExecuteReader();
            ids = new List<Guid>();

            while (reader.Read())
                ids.Add(reader.GetGuid(0));
        }
        catch
        {
            return 0;
        }

        var expired = 0;

        foreach (var id in ids)
        {
            var result = ExecuteTransaction(
                "question-expire",
                (connection, transaction) =>
                {
                    var row = LoadRow(
                        connection,
                        transaction,
                        WorkStateKind.Question,
                        id);

                    if (row is null ||
                        row.StateStatus != (int)QuestionStatus.Waiting ||
                        row.ExpiresAtUtc is null ||
                        row.ExpiresAtUtc.Value > _clock.UtcNow)
                    {
                        return Result.Success();
                    }

                    var question = RestoreQuestion(row);
                    var timeout = question.Timeout(_clock.UtcNow);

                    if (timeout.IsFailure)
                        return Result.Failure(timeout.Error!);

                    var updated = CreateQuestionRow(
                        timeout.Value!,
                        _clock.UtcNow);

                    var stateUpdate = UpdateState(
                        connection,
                        transaction,
                        row,
                        updated);

                    if (stateUpdate.IsFailure)
                        return stateUpdate;

                    return AppendEvent(
                        connection,
                        transaction,
                        updated,
                        new ResourceVersion(row.ResourceVersion),
                        "agent.question.timed-out");
                });

            if (result.IsSuccess)
                expired++;
        }

        return expired;
    }

    public Result<DelegationRequest> Submit(
        ResourceAccessContext requesterContext,
        DelegationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = RuntimeProtocolGuard.Validate(
            requesterContext,
            request.RequesterAgentId,
            request.RequesterRuntimeId);

        if (validation.IsFailure)
            return Result<DelegationRequest>.Failure(validation.Error!);

        if (request.Provenance.CreatedBy != requesterContext.PrincipalId!.Value)
        {
            return Result<DelegationRequest>.Failure(
                new Error(
                    "hive.agent.delegation.provenance-owner-mismatch",
                    ErrorCategory.Forbidden,
                    "Delegation provenance does not belong to the submitting principal."));
        }

        var row = new StateRow(
            WorkStateKind.Delegation,
            request.Id.Value,
            request.RequesterAgentId.Value,
            request.RequesterRuntimeId.Value,
            requesterContext.PrincipalId.Value.Value,
            (int)ResourceScopeKind.Runtime,
            request.RequesterRuntimeId.Value,
            ResourceVersion.Initial.Value,
            request.Provenance.CreatedAtUtc,
            request.Provenance.CorrelationId.Value,
            request.Provenance.CausationId?.Value,
            request.Source?.Kind is { } sourceKind
                ? (ResourceKind?)sourceKind
                : null,
            request.Source?.Identity is { } sourceIdentity
                ? (Guid?)sourceIdentity
                : null,
            ResourceLifecycleStatus.Active,
            request.Provenance.CreatedAtUtc,
            0,
            null,
            null,
            Serialize(new DelegationDocument(
                request.RequesterAgentId.Value,
                request.RequesterRuntimeId.Value,
                request.DelegateAgentId.Value,
                request.DelegateRuntimeId.Value,
                request.Task,
                ToReferenceDocument(request.Source))),
            request.Provenance.CreatedAtUtc);

        var persisted = ExecuteTransaction(
            "delegation-submit",
            (connection, transaction) =>
            {
                InsertState(connection, transaction, row);

                return AppendEvent(
                    connection,
                    transaction,
                    row,
                    null,
                    "agent.delegation.submitted");
            });

        return persisted.IsFailure
            ? Result<DelegationRequest>.Failure(persisted.Error!)
            : Result<DelegationRequest>.Success(request);
    }

    public Result<DelegationRequest> Read(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        DelegationId delegationId)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<DelegationRequest>.Failure(validation.Error!);

        try
        {
            var row = LoadRow(
                WorkStateKind.Delegation,
                delegationId.Value);

            if (row is null)
            {
                return Result<DelegationRequest>.Failure(
                    NotFound(
                        "hive.agent.delegation.not-found",
                        "The requested delegation does not exist."));
            }

            var request = RestoreDelegation(row);
            var requester =
                request.RequesterAgentId == agentId &&
                request.RequesterRuntimeId == runtimeId;
            var delegateRuntime =
                request.DelegateAgentId == agentId &&
                request.DelegateRuntimeId == runtimeId;

            if (!requester && !delegateRuntime)
            {
                return Result<DelegationRequest>.Failure(
                    new Error(
                        "hive.agent.delegation.not-authorized",
                        ErrorCategory.Forbidden,
                        "Only the requesting or delegated RuntimeInstance may read the delegation."));
            }

            return Result<DelegationRequest>.Success(request);
        }
        catch (SqlException exception)
        {
            return Result<DelegationRequest>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.agent-work-state.read-delegation-sql",
                    "The durable delegation read failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<DelegationRequest>.Failure(
                HivePersistenceError.Internal(
                    "hive.persistence.agent-work-state.read-delegation",
                    "The durable delegation record could not be reconstructed.",
                    exception));
        }
    }

    private Result<Question> GetQuestion(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        QuestionId questionId)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<Question>.Failure(validation.Error!);

        try
        {
            var row = LoadRow(
                WorkStateKind.Question,
                questionId.Value);

            if (row is null)
            {
                return Result<Question>.Failure(
                    NotFound(
                        "hive.agent.question.not-found",
                        "The requested Question does not exist in this RuntimeInstance."));
            }

            var accessError = ValidateRowAccess(
                row,
                accessContext,
                agentId,
                runtimeId,
                "hive.agent.question.runtime-mismatch",
                "The requested Question belongs to another runtime boundary.");

            return accessError is not null
                ? Result<Question>.Failure(accessError)
                : Result<Question>.Success(RestoreQuestion(row));
        }
        catch (SqlException exception)
        {
            return Result<Question>.Failure(
                HivePersistenceError.External(
                    "hive.persistence.agent-work-state.get-question-sql",
                    "The durable Question read failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<Question>.Failure(
                HivePersistenceError.Internal(
                    "hive.persistence.agent-work-state.get-question",
                    "The durable Question state could not be reconstructed.",
                    exception));
        }
    }

    private Result<Objective> TransitionObjective(
        string operation,
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        string eventType,
        DateTimeOffset changedAtUtc,
        Func<Objective, Result<Objective>> transition)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<Objective>.Failure(validation.Error!);

        try
        {
            return ExecuteTransaction(
                operation,
                (connection, transaction) =>
                {
                    var row = LoadRow(
                        connection,
                        transaction,
                        WorkStateKind.Objective,
                        objectiveId.Value);

                    if (row is null)
                    {
                        return Result<Objective>.Failure(
                            NotFound(
                                "hive.agent.objective.not-found",
                                "The requested objective does not exist in this RuntimeInstance."));
                    }

                    var accessError = ValidateRowAccess(
                        row,
                        accessContext,
                        agentId,
                        runtimeId,
                        "hive.agent.objective.runtime-mismatch",
                        "The requested objective belongs to another runtime boundary.");

                    if (accessError is not null)
                        return Result<Objective>.Failure(accessError);

                    var objective = RestoreObjective(row);
                    var transitioned = transition(objective);

                    if (transitioned.IsFailure)
                        return transitioned;

                    if (ReferenceEquals(transitioned.Value, objective))
                        return transitioned;

                    var updated = transitioned.Value!;
                    var updatedRow = CreateResourceRow(
                        WorkStateKind.Objective,
                        updated.AgentId,
                        updated.RuntimeId,
                        updated.Resource.Owner,
                        updated.Resource.Scope,
                        updated.Resource.Version,
                        updated.Resource.Provenance,
                        updated.Resource.Lifecycle,
                        updated.Id.Value,
                        (int)updated.Status,
                        null,
                        null,
                        Serialize(new ObjectiveDocument(
                            updated.Title,
                            updated.CompletionCriteria,
                            updated.Priority,
                            updated.DeadlineUtc,
                            updated.Dependencies
                                .Select(static value => value.Value)
                                .ToArray(),
                            ToBindingDocument(updated.WorkItemBinding))),
                        changedAtUtc);

                    var stateUpdate = UpdateState(
                        connection,
                        transaction,
                        row,
                        updatedRow);

                    if (stateUpdate.IsFailure)
                        return Result<Objective>.Failure(stateUpdate.Error!);

                    var appended = AppendEvent(
                        connection,
                        transaction,
                        updatedRow,
                        new ResourceVersion(row.ResourceVersion),
                        eventType);

                    return appended.IsFailure
                        ? Result<Objective>.Failure(appended.Error!)
                        : Result<Objective>.Success(updated);
                });
        }
        catch (SqlException exception)
        {
            return Result<Objective>.Failure(
                HivePersistenceError.External(
                    $"hive.persistence.agent-work-state.{operation}-sql",
                    "The durable Objective operation failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<Objective>.Failure(
                HivePersistenceError.Internal(
                    $"hive.persistence.agent-work-state.{operation}",
                    "The durable Objective operation failed.",
                    exception));
        }
    }

    private Result<Question> TransitionQuestion(
        string operation,
        ResourceAccessContext accessContext,
        QuestionId questionId,
        AgentId agentId,
        RuntimeId runtimeId,
        string eventType,
        DateTimeOffset changedAtUtc,
        Func<Question, Result<Question>> transition)
    {
        var validation = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (validation.IsFailure)
            return Result<Question>.Failure(validation.Error!);

        try
        {
            return ExecuteTransaction(
                operation,
                (connection, transaction) =>
                {
                    var row = LoadRow(
                        connection,
                        transaction,
                        WorkStateKind.Question,
                        questionId.Value);

                    if (row is null)
                    {
                        return Result<Question>.Failure(
                            NotFound(
                                "hive.agent.question.not-found",
                                "The requested Question does not exist in this RuntimeInstance."));
                    }

                    var accessError = ValidateRowAccess(
                        row,
                        accessContext,
                        agentId,
                        runtimeId,
                        "hive.agent.question.runtime-mismatch",
                        "The requested Question belongs to another runtime boundary.");

                    if (accessError is not null)
                        return Result<Question>.Failure(accessError);

                    var question = RestoreQuestion(row);
                    var transitioned = transition(question);

                    if (transitioned.IsFailure)
                        return transitioned;

                    var updated = transitioned.Value!;
                    var updatedRow = CreateQuestionRow(
                        updated,
                        changedAtUtc);

                    var stateUpdate = UpdateState(
                        connection,
                        transaction,
                        row,
                        updatedRow);

                    if (stateUpdate.IsFailure)
                        return Result<Question>.Failure(stateUpdate.Error!);

                    var appended = AppendEvent(
                        connection,
                        transaction,
                        updatedRow,
                        new ResourceVersion(row.ResourceVersion),
                        eventType);

                    return appended.IsFailure
                        ? Result<Question>.Failure(appended.Error!)
                        : Result<Question>.Success(updated);
                });
        }
        catch (SqlException exception)
        {
            return Result<Question>.Failure(
                HivePersistenceError.External(
                    $"hive.persistence.agent-work-state.{operation}-sql",
                    "The durable Question operation failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<Question>.Failure(
                HivePersistenceError.Internal(
                    $"hive.persistence.agent-work-state.{operation}",
                    "The durable Question operation failed.",
                    exception));
        }
    }

    private Result<T> ExecuteTransaction<T>(
        string operation,
        Func<SqlConnection, SqlTransaction, Result<T>> action)
    {
        try
        {
            using var connection = OpenConnection();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable, deferred: false);

            var result = action(
                connection,
                transaction);

            if (result.IsFailure)
            {
                transaction.Rollback();
                return result;
            }

            transaction.Commit();
            return result;
        }
        catch (SqlException exception)
        {
            return Result<T>.Failure(
                HivePersistenceError.External(
                    $"hive.persistence.agent-work-state.{operation}-sql",
                    "The durable Agent work-state operation failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(
                HivePersistenceError.Internal(
                    $"hive.persistence.agent-work-state.{operation}",
                    "The durable Agent work-state operation failed.",
                    exception));
        }
    }

    private Result ExecuteTransaction(
        string operation,
        Func<SqlConnection, SqlTransaction, Result> action)
    {
        try
        {
            using var connection = OpenConnection();

            using var transaction =
                connection.BeginTransaction(IsolationLevel.Serializable);

            var result = action(
                connection,
                transaction);

            if (result.IsFailure)
            {
                transaction.Rollback();
                return result;
            }

            transaction.Commit();
            return result;
        }
        catch (SqlException exception)
        {
            return Result.Failure(
                HivePersistenceError.External(
                    $"hive.persistence.agent-work-state.{operation}-sql",
                    "The durable Agent work-state operation failed at the Embedded SQLite boundary.",
                    exception));
        }
        catch (Exception exception)
        {
            return Result.Failure(
                HivePersistenceError.Internal(
                    $"hive.persistence.agent-work-state.{operation}",
                    "The durable Agent work-state operation failed.",
                    exception));
        }
    }

    private void InsertState(
        SqlConnection connection,
        SqlTransaction transaction,
        StateRow row)
    {
        using var command = CreateCommand(
            connection,
            """
            INSERT INTO [HiveAgentWorkState]
            (
                [WorkStateKind],
                [StateId],
                [AgentId],
                [RuntimeId],
                [OwnerPrincipalId],
                [ScopeKind],
                [ScopeIdentity],
                [ResourceVersion],
                [CreatedAtUtc],
                [CorrelationId],
                [CausationId],
                [SourceKind],
                [SourceIdentity],
                [LifecycleStatus],
                [LifecycleChangedAtUtc],
                [StateStatus],
                [StateKey],
                [ExpiresAtUtc],
                [StateJson],
                [UpdatedAtUtc]
            )
            VALUES
            (
                @Kind,
                @StateId,
                @AgentId,
                @RuntimeId,
                @OwnerPrincipalId,
                @ScopeKind,
                @ScopeIdentity,
                @ResourceVersion,
                @CreatedAtUtc,
                @CorrelationId,
                @CausationId,
                @SourceKind,
                @SourceIdentity,
                @LifecycleStatus,
                @LifecycleChangedAtUtc,
                @StateStatus,
                @StateKey,
                @ExpiresAtUtc,
                @StateJson,
                @UpdatedAtUtc
            );
            """,
            transaction);

        AddRowParameters(
            command,
            row);

        command.ExecuteNonQuery();
    }

    private Result UpdateState(
        SqlConnection connection,
        SqlTransaction transaction,
        StateRow expected,
        StateRow updated)
    {
        using var command = CreateCommand(
            connection,
            """
            UPDATE [HiveAgentWorkState]
            SET [AgentId] = @AgentId,
                [RuntimeId] = @RuntimeId,
                [OwnerPrincipalId] = @OwnerPrincipalId,
                [ScopeKind] = @ScopeKind,
                [ScopeIdentity] = @ScopeIdentity,
                [ResourceVersion] = @ResourceVersion,
                [CreatedAtUtc] = @CreatedAtUtc,
                [CorrelationId] = @CorrelationId,
                [CausationId] = @CausationId,
                [SourceKind] = @SourceKind,
                [SourceIdentity] = @SourceIdentity,
                [LifecycleStatus] = @LifecycleStatus,
                [LifecycleChangedAtUtc] = @LifecycleChangedAtUtc,
                [StateStatus] = @StateStatus,
                [StateKey] = @StateKey,
                [ExpiresAtUtc] = @ExpiresAtUtc,
                [StateJson] = @StateJson,
                [UpdatedAtUtc] = @UpdatedAtUtc
            WHERE [WorkStateKind] = @ExpectedKind
              AND [StateId] = @ExpectedStateId
              AND [ResourceVersion] = @ExpectedResourceVersion;
            """,
            transaction);

        AddRowParameters(
            command,
            updated);

        command.Parameters.Add(
            IntParameter(
                "@ExpectedKind",
                (int)expected.Kind));

        command.Parameters.Add(
            GuidParameter(
                "@ExpectedStateId",
                expected.StateId));

        command.Parameters.Add(
            BigIntParameter(
                "@ExpectedResourceVersion",
                expected.ResourceVersion));

        return command.ExecuteNonQuery() == 1
            ? Result.Success()
            : Result.Failure(
                Error.Concurrency(
                    "hive.agent.work-state.concurrency-conflict",
                    "The durable Agent work state changed before the requested update was committed."));
    }

    private Result AppendEvent(
        SqlConnection connection,
        SqlTransaction transaction,
        StateRow row,
        ResourceVersion? expectedVersion,
        string eventType)
    {
        var streamKind = row.Kind switch
        {
            WorkStateKind.Objective => ResourceKind.Objective,
            WorkStateKind.Memory => ResourceKind.Memory,
            WorkStateKind.Question => ResourceKind.Question,
            WorkStateKind.Delegation => ResourceKind.Delegation,
            _ => throw new ArgumentOutOfRangeException()
        };

        var state = JsonSerializer.Deserialize<JsonElement>(
            row.StateJson,
            JsonOptions);

        var envelope = EventEnvelope.Create(
            EventId.New(),
            row.UpdatedAtUtc,
            new EventType(eventType),
            new EventPayloadVersion(PayloadSchemaVersion),
            new CorrelationId(row.CorrelationId),
            row.CausationId is { } causationId
                ? new CausationId(causationId)
                : null,
            state);

        var stream = new ResourceReference(
            streamKind,
            row.StateId);

        var snapshot = new EventSnapshot(
            stream,
            new ResourceVersion(row.ResourceVersion),
            new EventPayloadVersion(PayloadSchemaVersion),
            state);

        var request = new EventAppendRequest(
            stream,
            expectedVersion,
            envelope,
            snapshot);

        var result = _eventStore.AppendInTransactionAsync(
                connection,
                transaction,
                request,
                CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        return result.IsSuccess
            ? Result.Success()
            : Result.Failure(result.Error!);
    }

    private StateRow? LoadRow(
        SqlConnection connection,
        SqlTransaction? transaction,
        WorkStateKind kind,
        Guid stateId)
    {
        using var command = CreateCommand(
            connection,
            """
            SELECT
                [WorkStateKind],
                [StateId],
                [AgentId],
                [RuntimeId],
                [OwnerPrincipalId],
                [ScopeKind],
                [ScopeIdentity],
                [ResourceVersion],
                [CreatedAtUtc],
                [CorrelationId],
                [CausationId],
                [SourceKind],
                [SourceIdentity],
                [LifecycleStatus],
                [LifecycleChangedAtUtc],
                [StateStatus],
                [StateKey],
                [ExpiresAtUtc],
                [StateJson],
                [UpdatedAtUtc]
            FROM [HiveAgentWorkState]
            WHERE [WorkStateKind] = @Kind
              AND [StateId] = @StateId;
            """,
            transaction);

        command.Parameters.Add(
            IntParameter(
                "@Kind",
                (int)kind));
        command.Parameters.Add(
            GuidParameter(
                "@StateId",
                stateId));

        using var reader = command.ExecuteReader();

        return reader.Read()
            ? ReadRow(reader)
            : null;
    }

    private StateRow? LoadRow(
        WorkStateKind kind,
        Guid stateId)
    {
        using var connection = OpenConnection();

        return LoadRow(
            connection,
            null,
            kind,
            stateId);
    }

    private static StateRow ReadRow(SqlDataReader reader)
    {
        var sourceKind = reader.IsDBNull(
            reader.GetOrdinal("SourceKind"))
            ? null
            : (ResourceKind?)reader.GetInt32(
                reader.GetOrdinal("SourceKind"));

        var sourceIdentity = reader.IsDBNull(
            reader.GetOrdinal("SourceIdentity"))
            ? (Guid?)null
            : reader.GetGuid(
                reader.GetOrdinal("SourceIdentity"));

        return new StateRow(
            (WorkStateKind)reader.GetInt32(
                reader.GetOrdinal("WorkStateKind")),
            reader.GetGuid(
                reader.GetOrdinal("StateId")),
            reader.GetGuid(
                reader.GetOrdinal("AgentId")),
            reader.GetGuid(
                reader.GetOrdinal("RuntimeId")),
            reader.GetGuid(
                reader.GetOrdinal("OwnerPrincipalId")),
            reader.GetInt32(
                reader.GetOrdinal("ScopeKind")),
            reader.GetGuid(
                reader.GetOrdinal("ScopeIdentity")),
            reader.GetInt64(
                reader.GetOrdinal("ResourceVersion")),
            ReadUtc(
                reader,
                "CreatedAtUtc"),
            reader.GetGuid(
                reader.GetOrdinal("CorrelationId")),
            reader.IsDBNull(
                reader.GetOrdinal("CausationId"))
                ? null
                : reader.GetGuid(
                    reader.GetOrdinal("CausationId")),
            sourceKind,
            sourceIdentity,
            (ResourceLifecycleStatus)reader.GetInt32(
                reader.GetOrdinal("LifecycleStatus")),
            ReadUtc(
                reader,
                "LifecycleChangedAtUtc"),
            reader.GetInt32(
                reader.GetOrdinal("StateStatus")),
            reader.IsDBNull(
                reader.GetOrdinal("StateKey"))
                ? null
                : reader.GetString(
                    reader.GetOrdinal("StateKey")),
            reader.IsDBNull(
                reader.GetOrdinal("ExpiresAtUtc"))
                ? null
                : ReadUtc(
                    reader,
                    "ExpiresAtUtc"),
            reader.GetString(
                reader.GetOrdinal("StateJson")),
            ReadUtc(
                reader,
                "UpdatedAtUtc"));
    }

    private static DateTimeOffset ReadUtc(
        SqlDataReader reader,
        string column)
    {
        var value = reader.GetDateTime(
            reader.GetOrdinal(column));

        return new DateTimeOffset(
            DateTime.SpecifyKind(
                value,
                DateTimeKind.Utc));
    }

    private static StateRow CreateResourceRow(
        WorkStateKind kind,
        AgentId agentId,
        RuntimeId runtimeId,
        PrincipalId owner,
        ResourceScope scope,
        ResourceVersion version,
        ResourceProvenance provenance,
        ResourceLifecycle lifecycle,
        Guid stateId,
        int stateStatus,
        string? stateKey,
        DateTimeOffset? expiresAtUtc,
        string stateJson,
        DateTimeOffset? updatedAtUtc = null) =>
        new(
            kind,
            stateId,
            agentId.Value,
            runtimeId.Value,
            owner.Value,
            (int)scope.Kind,
            scope.Identity ?? throw new InvalidOperationException(
                "A runtime-scoped work state requires a scope identity."),
            version.Value,
            provenance.CreatedAtUtc,
            provenance.CorrelationId.Value,
            provenance.CausationId?.Value,
            provenance.Source?.Kind is { } sourceKind
                ? (ResourceKind?)sourceKind
                : null,
            provenance.Source?.Identity is { } sourceIdentity
                ? (Guid?)sourceIdentity
                : null,
            lifecycle.Status,
            lifecycle.ChangedAtUtc,
            stateStatus,
            stateKey,
            expiresAtUtc,
            stateJson,
            updatedAtUtc ?? lifecycle.ChangedAtUtc);

    private static StateRow CreateQuestionRow(
        Question question,
        DateTimeOffset? updatedAtUtc = null) =>
        CreateResourceRow(
            WorkStateKind.Question,
            question.AskedByAgentId,
            question.AskedByRuntimeId,
            question.Resource.Owner,
            question.Resource.Scope,
            question.Resource.Version,
            question.Resource.Provenance,
            question.Resource.Lifecycle,
            question.Id.Value,
            (int)question.Status,
            null,
            question.ExpiresAtUtc,
            Serialize(new QuestionDocument(
                question.Prompt,
                question.ExpiresAtUtc,
                (int)question.Status,
                question.Answer,
                question.AnsweredByAgentId?.Value,
                question.AnsweredByRuntimeId?.Value)),
            updatedAtUtc);

    private static ResourceEnvelope<ObjectiveId> RestoreObjectiveResource(
        StateRow row) =>
        RestoreResource(
            ResourceKind.Objective,
            new ObjectiveId(row.StateId),
            row);

    private static ResourceEnvelope<MemoryId> RestoreMemoryResource(
        StateRow row) =>
        RestoreResource(
            ResourceKind.Memory,
            new MemoryId(row.StateId),
            row);

    private static ResourceEnvelope<QuestionId> RestoreQuestionResource(
        StateRow row) =>
        RestoreResource(
            ResourceKind.Question,
            new QuestionId(row.StateId),
            row);

    private static ResourceEnvelope<TIdentity> RestoreResource<TIdentity>(
        ResourceKind kind,
        TIdentity identity,
        StateRow row)
        where TIdentity : struct =>
        new(
            kind,
            identity,
            new PrincipalId(row.OwnerPrincipalId),
            new ResourceScope(
                (ResourceScopeKind)row.ScopeKind,
                row.ScopeIdentity),
            new ResourceVersion(row.ResourceVersion),
            RestoreProvenance(row),
            new ResourceLifecycle(
                row.LifecycleStatus,
                row.LifecycleChangedAtUtc));

    private static Objective RestoreObjective(StateRow row)
    {
        var document = Deserialize<ObjectiveDocument>(
            row.StateJson);

        var status = (ObjectiveStatus)row.StateStatus;

        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException(
                "Stored Objective status is invalid.");
        }

        return Objective.Restore(
            RestoreObjectiveResource(row),
            new AgentId(row.AgentId),
            new RuntimeId(row.RuntimeId),
            document.Title,
            document.CompletionCriteria,
            document.Priority,
            document.DeadlineUtc,
            document.Dependencies
                .Select(static value => new ObjectiveId(value))
                .ToArray(),
            RestoreBinding(document.WorkItemBinding),
            status);
    }

    private static AgentMemoryEntry RestoreMemory(
        StateRow row)
    {
        var document = Deserialize<MemoryDocument>(
            row.StateJson);

        var evidenceKind =
            (MemoryEvidenceKind)document.EvidenceKind;

        if (!Enum.IsDefined(evidenceKind))
        {
            throw new InvalidOperationException(
                "Stored memory evidence kind is invalid.");
        }

        return AgentMemoryEntry.Restore(
            RestoreMemoryResource(row),
            new AgentId(row.AgentId),
            new RuntimeId(row.RuntimeId),
            document.Key,
            document.Content,
            evidenceKind);
    }

    private static Question RestoreQuestion(
        StateRow row)
    {
        var document = Deserialize<QuestionDocument>(
            row.StateJson);

        var status = (QuestionStatus)document.Status;

        if (!Enum.IsDefined(status))
        {
            throw new InvalidOperationException(
                "Stored Question status is invalid.");
        }

        return Question.Restore(
            RestoreQuestionResource(row),
            new AgentId(row.AgentId),
            new RuntimeId(row.RuntimeId),
            document.Prompt,
            document.ExpiresAtUtc,
            status,
            document.Answer,
            document.AnsweredByAgentId is { } agent
                ? new AgentId(agent)
                : null,
            document.AnsweredByRuntimeId is { } runtime
                ? new RuntimeId(runtime)
                : null);
    }

    private static DelegationRequest RestoreDelegation(
        StateRow row)
    {
        var document = Deserialize<DelegationDocument>(
            row.StateJson);

        var source = document.Source is { } reference
            ? (ResourceReference?)new ResourceReference(
                (ResourceKind)reference.Kind,
                reference.Identity)
            : null;

        return DelegationRequest.Restore(
            new DelegationId(row.StateId),
            new AgentId(document.RequesterAgentId),
            new RuntimeId(document.RequesterRuntimeId),
            new AgentId(document.DelegateAgentId),
            new RuntimeId(document.DelegateRuntimeId),
            document.Task,
            source,
            RestoreProvenance(row));
    }

    private static WorkItemBinding? RestoreBinding(
        BindingDocument? document) =>
        document is null
            ? null
            : WorkItemBinding.Restore(
                new WorkItemId(document.WorkItemId),
                new ResourceVersion(document.WorkItemVersion),
                new AgentId(document.AgentId),
                new RuntimeId(document.RuntimeId),
                RestoreBindingProvenance(document.Provenance));

    private static BindingDocument? ToBindingDocument(
        WorkItemBinding? binding) =>
        binding is null
            ? null
            : new BindingDocument(
                binding.WorkItemId.Value,
                binding.WorkItemVersion.Value,
                binding.AgentId.Value,
                binding.RuntimeId.Value,
                ToProvenanceDocument(
                    binding.Provenance));

    private static ProvenanceDocument ToProvenanceDocument(
        ResourceProvenance provenance) =>
        new(
            provenance.CreatedBy.Value,
            provenance.CreatedAtUtc,
            provenance.CorrelationId.Value,
            provenance.CausationId?.Value,
            ToReferenceDocument(
                provenance.Source));

    private static ResourceProvenance RestoreProvenance(
        StateRow row)
    {
        var source =
            row.SourceKind is { } sourceKind &&
            row.SourceIdentity is { } sourceIdentity
                ? (ResourceReference?)new ResourceReference(
                    sourceKind,
                    sourceIdentity)
                : null;

        return new ResourceProvenance(
            new PrincipalId(row.OwnerPrincipalId),
            row.CreatedAtUtc,
            new CorrelationId(row.CorrelationId),
            row.CausationId is { } causationId
                ? new CausationId(causationId)
                : null,
            source);
    }

    private static ResourceProvenance RestoreBindingProvenance(
        ProvenanceDocument document)
    {
        var source =
            document.Source is { } sourceReference
                ? (ResourceReference?)new ResourceReference(
                    (ResourceKind)sourceReference.Kind,
                    sourceReference.Identity)
                : null;

        return new ResourceProvenance(
            new PrincipalId(document.CreatedBy),
            document.CreatedAtUtc,
            new CorrelationId(document.CorrelationId),
            document.CausationId is { } causationId
                ? new CausationId(causationId)
                : null,
            source);
    }

    private static ResourceReferenceDocument? ToReferenceDocument(
        ResourceReference? reference) =>
        reference is null
            ? null
            : new ResourceReferenceDocument(
                (int)reference.Value.Kind,
                reference.Value.Identity);

    private static T Deserialize<T>(
        string json) =>
        JsonSerializer.Deserialize<T>(
            json,
            JsonOptions)
        ?? throw new InvalidOperationException(
            "Durable Agent work state payload is empty.");

    private static string Serialize<T>(
        T value) =>
        JsonSerializer.Serialize(
            value,
            JsonOptions);

    private static Error NotFound(
        string code,
        string message) =>
        new(
            code,
            ErrorCategory.NotFound,
            message);

    private static Error? ValidateRowAccess(
        StateRow row,
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string runtimeMismatchCode,
        string runtimeMismatchMessage)
    {
        if (row.AgentId != agentId.Value ||
            row.RuntimeId != runtimeId.Value)
        {
            return new Error(
                runtimeMismatchCode,
                ErrorCategory.Forbidden,
                runtimeMismatchMessage);
        }

        if (row.OwnerPrincipalId !=
            accessContext.PrincipalId!.Value.Value)
        {
            return new Error(
                "hive.agent.protocol.owner-mismatch",
                ErrorCategory.Forbidden,
                "The access context principal does not own the requested Agent resource.");
        }

        if (row.ScopeKind != (int)ResourceScopeKind.Runtime ||
            row.ScopeIdentity != runtimeId.Value)
        {
            return new Error(
                "hive.agent.protocol.scope-mismatch",
                ErrorCategory.Forbidden,
                "The durable Agent work state does not satisfy the requested Runtime scope.");
        }

        return null;
    }

    private SqlConnection OpenConnection() =>
        _options.OpenConnectionAsync().GetAwaiter().GetResult();

    private SqlCommand CreateCommand(
        SqlConnection connection,
        string sql,
        SqlTransaction? transaction = null) =>
        new(
            sql,
            connection,
            transaction)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

    private static SqlParameter GuidParameter(
        string name,
        Guid value) =>
        new(
            name,
            SqlDbType.UniqueIdentifier)
        {
            Value = value.ToString("D")
        };

    private static SqlParameter IntParameter(
        string name,
        int value) =>
        new(
            name,
            SqlDbType.Int)
        {
            Value = value
        };

    private static SqlParameter BigIntParameter(
        string name,
        long value) =>
        new(
            name,
            SqlDbType.BigInt)
        {
            Value = value
        };

    private static SqlParameter DateTimeParameter(
        string name,
        DateTimeOffset value) =>
        new(
            name,
            SqlDbType.DateTime2)
        {
            Value = value.UtcDateTime.ToString(
                "O",
                System.Globalization.CultureInfo.InvariantCulture)
        };

    private static void AddRowParameters(
        SqlCommand command,
        StateRow row)
    {
        command.Parameters.Add(
            IntParameter(
                "@Kind",
                (int)row.Kind));
        command.Parameters.Add(
            GuidParameter(
                "@StateId",
                row.StateId));
        command.Parameters.Add(
            GuidParameter(
                "@AgentId",
                row.AgentId));
        command.Parameters.Add(
            GuidParameter(
                "@RuntimeId",
                row.RuntimeId));
        command.Parameters.Add(
            GuidParameter(
                "@OwnerPrincipalId",
                row.OwnerPrincipalId));
        command.Parameters.Add(
            IntParameter(
                "@ScopeKind",
                row.ScopeKind));
        command.Parameters.Add(
            GuidParameter(
                "@ScopeIdentity",
                row.ScopeIdentity));
        command.Parameters.Add(
            BigIntParameter(
                "@ResourceVersion",
                row.ResourceVersion));
        command.Parameters.Add(
            DateTimeParameter(
                "@CreatedAtUtc",
                row.CreatedAtUtc));
        command.Parameters.Add(
            GuidParameter(
                "@CorrelationId",
                row.CorrelationId));
        command.Parameters.Add(
            row.CausationId is { } causation
                ? GuidParameter(
                    "@CausationId",
                    causation)
                : new SqlParameter(
                    "@CausationId",
                    SqlDbType.UniqueIdentifier)
                {
                    Value = DBNull.Value
                });
        command.Parameters.Add(
            row.SourceKind is { } sourceKind
                ? IntParameter(
                    "@SourceKind",
                    (int)sourceKind)
                : new SqlParameter(
                    "@SourceKind",
                    SqlDbType.Int)
                {
                    Value = DBNull.Value
                });
        command.Parameters.Add(
            row.SourceIdentity is { } sourceIdentity
                ? GuidParameter(
                    "@SourceIdentity",
                    sourceIdentity)
                : new SqlParameter(
                    "@SourceIdentity",
                    SqlDbType.UniqueIdentifier)
                {
                    Value = DBNull.Value
                });
        command.Parameters.Add(
            IntParameter(
                "@LifecycleStatus",
                (int)row.LifecycleStatus));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                row.LifecycleChangedAtUtc));
        command.Parameters.Add(
            IntParameter(
                "@StateStatus",
                row.StateStatus));
        command.Parameters.Add(
            row.StateKey is null
                ? new SqlParameter(
                    "@StateKey",
                    SqlDbType.NVarChar,
                    -1)
                {
                    Value = DBNull.Value
                }
                : new SqlParameter(
                    "@StateKey",
                    SqlDbType.NVarChar,
                    -1)
                {
                    Value = row.StateKey
                });
        command.Parameters.Add(
            row.ExpiresAtUtc is { } expires
                ? DateTimeParameter(
                    "@ExpiresAtUtc",
                    expires)
                : new SqlParameter(
                    "@ExpiresAtUtc",
                    SqlDbType.DateTime2)
                {
                    Value = DBNull.Value
                });
        command.Parameters.Add(
            new SqlParameter(
                "@StateJson",
                SqlDbType.NVarChar,
                -1)
            {
                Value = row.StateJson
            });
        command.Parameters.Add(
            DateTimeParameter(
                "@UpdatedAtUtc",
                row.UpdatedAtUtc));
    }

    private sealed record StateRow(
        WorkStateKind Kind,
        Guid StateId,
        Guid AgentId,
        Guid RuntimeId,
        Guid OwnerPrincipalId,
        int ScopeKind,
        Guid ScopeIdentity,
        long ResourceVersion,
        DateTimeOffset CreatedAtUtc,
        Guid CorrelationId,
        Guid? CausationId,
        ResourceKind? SourceKind,
        Guid? SourceIdentity,
        ResourceLifecycleStatus LifecycleStatus,
        DateTimeOffset LifecycleChangedAtUtc,
        int StateStatus,
        string? StateKey,
        DateTimeOffset? ExpiresAtUtc,
        string StateJson,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ResourceReferenceDocument(
        int Kind,
        Guid Identity);

    private sealed record ProvenanceDocument(
        Guid CreatedBy,
        DateTimeOffset CreatedAtUtc,
        Guid CorrelationId,
        Guid? CausationId,
        ResourceReferenceDocument? Source);

    private sealed record BindingDocument(
        Guid WorkItemId,
        long WorkItemVersion,
        Guid AgentId,
        Guid RuntimeId,
        ProvenanceDocument Provenance);

    private sealed record ObjectiveDocument(
        string Title,
        string CompletionCriteria,
        int Priority,
        DateTimeOffset? DeadlineUtc,
        Guid[] Dependencies,
        BindingDocument? WorkItemBinding);

    private sealed record MemoryDocument(
        string Key,
        string Content,
        int EvidenceKind,
        BindingDocument? WorkItemBinding);

    private sealed record QuestionDocument(
        string Prompt,
        DateTimeOffset ExpiresAtUtc,
        int Status,
        string? Answer,
        Guid? AnsweredByAgentId,
        Guid? AnsweredByRuntimeId);

    private sealed record DelegationDocument(
        Guid RequesterAgentId,
        Guid RequesterRuntimeId,
        Guid DelegateAgentId,
        Guid DelegateRuntimeId,
        string Task,
        ResourceReferenceDocument? Source);

    private sealed record EventStatePayload(
        JsonElement State);
}
