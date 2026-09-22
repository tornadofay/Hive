using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlWorkItemResourceStore : IWorkItemResourceStore
{
    private const string WorkItemColumns = """
        [WorkItemId],
        [OwnerPrincipalId],
        [ScopeKind],
        [ScopeIdentity],
        [ResourceVersion],
        [CreatedByPrincipalId],
        [CreatedAtUtc],
        [CorrelationId],
        [CausationId],
        [SourceKind],
        [SourceIdentity],
        [LifecycleStatus],
        [LifecycleChangedAtUtc],
        [Status],
        [MetadataJson],
        [AttachmentFileName],
        [AttachmentMediaType],
        [AttachmentContentLength],
        [AttachmentSha256]
        """;

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.General);

    private readonly HiveDatabaseOptions _options;
    private readonly SqlEventPersistenceStore _eventStore;

    public SqlWorkItemResourceStore(HiveDatabaseOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _eventStore = new SqlEventPersistenceStore(options);
    }

    public Task<Result<WorkItem>> CreateImageWorkItemAsync(
        WorkItemImageSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(
            "work item",
            cancellationToken,
            async (connection, transaction) =>
            {
                ArgumentNullException.ThrowIfNull(submission);
                ValidateAccessContext(accessContext);

                var now = DateTimeOffset.UtcNow;
                var attachment = new WorkItemAttachmentMetadata(
                    submission.FileName,
                    submission.MediaType,
                    submission.Content.LongLength,
                    Convert.ToHexString(
                        SHA256.HashData(submission.Content))
                        .ToLowerInvariant());

                var workItem = WorkItem.Create(
                    WorkItemId.New(),
                    accessContext.PrincipalId!.Value,
                    CreateScope(accessContext),
                    new ResourceProvenance(
                        accessContext.PrincipalId.Value,
                        now,
                        CorrelationId.New()),
                    now,
                    attachment: attachment);

                await InsertWorkItemAsync(
                    connection,
                    transaction,
                    workItem,
                    cancellationToken).ConfigureAwait(false);

                await InsertAttachmentAsync(
                    connection,
                    transaction,
                    workItem.Id,
                    submission,
                    attachment,
                    now,
                    cancellationToken).ConfigureAwait(false);

                var eventEnvelope = CreateEvent(
                    "work-item.created",
                    workItem,
                    null,
                    null);

                var append = await _eventStore.AppendInTransactionAsync(
                    connection,
                    transaction,
                    CreateAppendRequest(workItem, eventEnvelope),
                    cancellationToken).ConfigureAwait(false);

                if (append.IsFailure)
                    return Result<WorkItem>.Failure(append.Error!);

                return Result<WorkItem>.Success(workItem);
            });

    public async Task<Result<WorkItem>> GetWorkItemAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ValidateAccessContext(accessContext);

        if (workItemId == default)
        {
            return Result<WorkItem>.Failure(
                Error.Validation(
                    "hive.work-item.identity-required",
                    "WorkItem identity is required."));
        }

        try
        {
            await using var connection =
                await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                $"""
                SELECT {WorkItemColumns}
                FROM [dbo].[HiveWorkItems]
                WHERE [WorkItemId] = @WorkItemId;
                """);

            command.Parameters.Add(
                GuidParameter("@WorkItemId", workItemId.Value));

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result<WorkItem>.Failure(
                    NotFound(
                        "hive.work-item.not-found",
                        "The requested WorkItem does not exist."));
            }

            var workItem = ReadWorkItem(reader);
            var accessError = ValidateAccess(workItem.Resource, accessContext);

            return accessError is null
                ? Result<WorkItem>.Success(workItem)
                : Result<WorkItem>.Failure(accessError);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception)
        {
            return Result<WorkItem>.Failure(ToSqlError(exception));
        }
        catch (Exception exception)
        {
            return Result<WorkItem>.Failure(ToInvalidStateError(exception));
        }
    }

    public async Task<Result<IReadOnlyList<WorkItem>>> ListWorkItemsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default)
    {
        ValidateAccessContext(accessContext);

        try
        {
            await using var connection =
                await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                $"""
                SELECT {WorkItemColumns}
                FROM [dbo].[HiveWorkItems]
                WHERE [OwnerPrincipalId] = @PrincipalId
                  AND {ScopeAccessPredicate}
                {(includeRetired
                    ? string.Empty
                    : "AND [LifecycleStatus] <> @RetiredLifecycle")}
                ORDER BY [CreatedAtUtc] DESC, [WorkItemId];
                """);

            AddAccessParameters(command, accessContext);

            if (!includeRetired)
            {
                command.Parameters.Add(
                    IntParameter(
                        "@RetiredLifecycle",
                        (int)ResourceLifecycleStatus.Retired));
            }

            var items = new List<WorkItem>();

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var workItem = ReadWorkItem(reader);

                if (ValidateAccess(workItem.Resource, accessContext) is null)
                    items.Add(workItem);
            }

            return Result<IReadOnlyList<WorkItem>>.Success(items);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception)
        {
            return Result<IReadOnlyList<WorkItem>>.Failure(ToSqlError(exception));
        }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<WorkItem>>.Failure(ToInvalidStateError(exception));
        }
    }

    public async Task<Result<WorkItemAttachmentContent>> GetWorkItemAttachmentAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var workItem = await GetWorkItemAsync(
            workItemId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (workItem.IsFailure)
            return Result<WorkItemAttachmentContent>.Failure(workItem.Error!);

        if (workItem.Value!.Attachment is null)
        {
            return Result<WorkItemAttachmentContent>.Failure(
                Error.NotFound(
                    "hive.work-item.attachment-not-found",
                    "The requested WorkItem has no attachment."));
        }

        try
        {
            await using var connection =
                await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                """
                SELECT
                    [FileName],
                    [MediaType],
                    [ContentLength],
                    [Sha256],
                    [Content]
                FROM [dbo].[HiveWorkItemAttachments]
                WHERE [WorkItemId] = @WorkItemId;
                """);

            command.Parameters.Add(
                GuidParameter("@WorkItemId", workItemId.Value));

            await using var reader =
                await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return Result<WorkItemAttachmentContent>.Failure(
                    NotFound(
                        "hive.work-item.attachment-not-found",
                        "The requested WorkItem attachment does not exist."));
            }

            var metadata = new WorkItemAttachmentMetadata(
                reader.GetString(reader.GetOrdinal("FileName")),
                reader.GetString(reader.GetOrdinal("MediaType")),
                reader.GetInt64(reader.GetOrdinal("ContentLength")),
                reader.GetString(reader.GetOrdinal("Sha256")));

            var bytes = (byte[])reader["Content"];

            return Result<WorkItemAttachmentContent>.Success(
                new WorkItemAttachmentContent(metadata, bytes));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception)
        {
            return Result<WorkItemAttachmentContent>.Failure(ToSqlError(exception));
        }
        catch (Exception exception)
        {
            return Result<WorkItemAttachmentContent>.Failure(ToInvalidStateError(exception));
        }
    }

    public async Task<Result<IReadOnlyList<EventEnvelope>>> GetWorkItemActivityAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var workItem = await GetWorkItemAsync(
            workItemId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (workItem.IsFailure)
            return Result<IReadOnlyList<EventEnvelope>>.Failure(workItem.Error!);

        var stream = new ResourceReference(
            ResourceKind.WorkItem,
            workItemId.Value);

        var events = await _eventStore.ReadEventsAsync(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return events.IsSuccess
            ? Result<IReadOnlyList<EventEnvelope>>.Success(
                events.Value!.Select(static item => item.Envelope).ToArray())
            : Result<IReadOnlyList<EventEnvelope>>.Failure(events.Error!);
    }

    public Task<Result<WorkItem>> RequestApprovalAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            workItemId,
            expectedVersion,
            accessContext,
            WorkItemStatus.PendingApproval,
            "work-item.approval-requested",
            null,
            static item => item.Status is
                WorkItemStatus.Created or
                WorkItemStatus.Queued or
                WorkItemStatus.Running,
            cancellationToken);

    public Task<Result<WorkItem>> ApproveAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            workItemId,
            expectedVersion,
            accessContext,
            WorkItemStatus.Completed,
            "work-item.approved",
            null,
            static item => item.Status == WorkItemStatus.PendingApproval,
            cancellationToken);

    public Task<Result<WorkItem>> RejectAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string reason,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            workItemId,
            expectedVersion,
            accessContext,
            WorkItemStatus.Rejected,
            "work-item.rejected",
            reason,
            static item => item.Status == WorkItemStatus.PendingApproval,
            cancellationToken);

    private Task<Result<WorkItem>> TransitionAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        WorkItemStatus nextStatus,
        string eventType,
        string? reason,
        Func<WorkItem, bool> canTransition,
        CancellationToken cancellationToken) =>
        ExecuteInTransactionAsync(
            "work item",
            cancellationToken,
            async (connection, transaction) =>
            {
                ValidateAccessContext(accessContext);

                if (workItemId == default)
                {
                    return Result<WorkItem>.Failure(
                        Error.Validation(
                            "hive.work-item.identity-required",
                            "WorkItem identity is required."));
                }

                if (expectedVersion.Value <= 0)
                {
                    return Result<WorkItem>.Failure(
                        Error.Validation(
                            "hive.work-item.version-invalid",
                            "A positive expected WorkItem version is required."));
                }

                var current = await LoadWorkItemAsync(
                    connection,
                    transaction,
                    workItemId,
                    cancellationToken).ConfigureAwait(false);

                if (current is null)
                {
                    return Result<WorkItem>.Failure(
                        NotFound(
                            "hive.work-item.not-found",
                            "The requested WorkItem does not exist."));
                }

                var accessError = ValidateAccess(
                    current.Resource,
                    accessContext);

                if (accessError is not null)
                    return Result<WorkItem>.Failure(accessError);

                if (current.Resource.Version != expectedVersion)
                {
                    return Result<WorkItem>.Failure(
                        Error.Concurrency(
                            "hive.work-item.concurrency",
                            "The WorkItem changed before the requested transition was applied."));
                }

                if (!canTransition(current))
                {
                    return Result<WorkItem>.Failure(
                        Error.Conflict(
                            "hive.work-item.invalid-transition",
                            $"The WorkItem cannot transition from '{current.Status}' to '{nextStatus}'."));
                }

                var changedAt = DateTimeOffset.UtcNow;
                var updated = current.TransitionTo(nextStatus, changedAt);

                await UpdateWorkItemAsync(
                    connection,
                    transaction,
                    current,
                    updated,
                    cancellationToken).ConfigureAwait(false);

                var envelope = CreateEvent(
                    eventType,
                    updated,
                    current.Status,
                    reason);

                var append = await _eventStore.AppendInTransactionAsync(
                    connection,
                    transaction,
                    new EventAppendRequest(
                        new ResourceReference(
                            ResourceKind.WorkItem,
                            updated.Id.Value),
                        current.Resource.Version,
                        envelope,
                        CreateSnapshot(updated)),
                    cancellationToken).ConfigureAwait(false);

                if (append.IsFailure)
                    return Result<WorkItem>.Failure(append.Error!);

                return Result<WorkItem>.Success(updated);
            });

    private async Task InsertWorkItemAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkItem workItem,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            INSERT INTO [dbo].[HiveWorkItems]
            (
                {WorkItemColumns}
            )
            VALUES
            (
                @WorkItemId,
                @OwnerPrincipalId,
                @ScopeKind,
                @ScopeIdentity,
                @ResourceVersion,
                @CreatedByPrincipalId,
                @CreatedAtUtc,
                @CorrelationId,
                @CausationId,
                @SourceKind,
                @SourceIdentity,
                @LifecycleStatus,
                @LifecycleChangedAtUtc,
                @Status,
                @MetadataJson,
                @AttachmentFileName,
                @AttachmentMediaType,
                @AttachmentContentLength,
                @AttachmentSha256
            );
            """,
            transaction);

        AddWorkItemParameters(command, workItem);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpdateWorkItemAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkItem current,
        WorkItem updated,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            UPDATE [dbo].[HiveWorkItems]
            SET
                [ResourceVersion] = @NewVersion,
                [LifecycleStatus] = @LifecycleStatus,
                [LifecycleChangedAtUtc] = @LifecycleChangedAtUtc,
                [Status] = @Status,
                [MetadataJson] = @MetadataJson,
                [AttachmentFileName] = @AttachmentFileName,
                [AttachmentMediaType] = @AttachmentMediaType,
                [AttachmentContentLength] = @AttachmentContentLength,
                [AttachmentSha256] = @AttachmentSha256
            WHERE [WorkItemId] = @WorkItemId
              AND [ResourceVersion] = @ExpectedVersion;
            """,
            transaction);

        AddWorkItemStateParameters(command, updated);
        command.Parameters.Add(
            GuidParameter("@WorkItemId", updated.Id.Value));
        command.Parameters.Add(
            BigIntParameter("@ExpectedVersion", current.Resource.Version.Value));

        if (await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
        {
            throw new ConcurrencyException();
        }
    }

    private async Task InsertAttachmentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkItemId workItemId,
        WorkItemImageSubmission submission,
        WorkItemAttachmentMetadata attachment,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            INSERT INTO [dbo].[HiveWorkItemAttachments]
            (
                [WorkItemId],
                [FileName],
                [MediaType],
                [ContentLength],
                [Sha256],
                [Content],
                [CreatedAtUtc]
            )
            VALUES
            (
                @WorkItemId,
                @FileName,
                @MediaType,
                @ContentLength,
                @Sha256,
                @Content,
                @CreatedAtUtc
            );
            """,
            transaction);

        command.Parameters.Add(
            GuidParameter("@WorkItemId", workItemId.Value));
        command.Parameters.Add(
            TextParameter("@FileName", 260, submission.FileName));
        command.Parameters.Add(
            TextParameter("@MediaType", 200, submission.MediaType));
        command.Parameters.Add(
            BigIntParameter("@ContentLength", attachment.ContentLength));
        command.Parameters.Add(
            TextParameter("@Sha256", 64, attachment.Sha256));
        command.Parameters.Add(
            new SqlParameter("@Content", SqlDbType.VarBinary, -1)
            {
                Value = submission.Content
            });
        command.Parameters.Add(
            DateTimeParameter("@CreatedAtUtc", createdAtUtc));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<WorkItem?> LoadWorkItemAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        WorkItemId id,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            $"""
            SELECT {WorkItemColumns}
            FROM [dbo].[HiveWorkItems] WITH (UPDLOCK, HOLDLOCK)
            WHERE [WorkItemId] = @WorkItemId;
            """,
            transaction);

        command.Parameters.Add(
            GuidParameter("@WorkItemId", id.Value));

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ReadWorkItem(reader)
            : null;
    }

    private static WorkItem ReadWorkItem(SqlDataReader reader)
    {
        var scopeKind = (ResourceScopeKind)reader.GetInt32(
            reader.GetOrdinal("ScopeKind"));

        var scopeIdentity = reader.IsDBNull(reader.GetOrdinal("ScopeIdentity"))
            ? null
            : reader.GetGuid(reader.GetOrdinal("ScopeIdentity"));

        var scope = scopeKind switch
        {
            ResourceScopeKind.Global when scopeIdentity is null =>
                ResourceScope.Global(),
            ResourceScopeKind.Tenant when scopeIdentity is not null =>
                ResourceScope.Tenant(new TenantId(scopeIdentity.Value)),
            ResourceScopeKind.User when scopeIdentity is not null =>
                ResourceScope.User(new UserId(scopeIdentity.Value)),
            ResourceScopeKind.Workspace when scopeIdentity is not null =>
                ResourceScope.Workspace(new WorkspaceId(scopeIdentity.Value)),
            ResourceScopeKind.Agent when scopeIdentity is not null =>
                ResourceScope.Agent(new AgentId(scopeIdentity.Value)),
            ResourceScopeKind.Runtime when scopeIdentity is not null =>
                ResourceScope.Runtime(new RuntimeId(scopeIdentity.Value)),
            ResourceScopeKind.Execution when scopeIdentity is not null =>
                ResourceScope.Execution(new ExecutionId(scopeIdentity.Value)),
            _ => throw new InvalidOperationException(
                "Persisted WorkItem scope state is invalid.")
        };

        var sourceKind = reader.IsDBNull(reader.GetOrdinal("SourceKind"))
            ? null
            : (ResourceKind?)reader.GetInt32(reader.GetOrdinal("SourceKind"));

        Guid? sourceIdentity = reader.IsDBNull(reader.GetOrdinal("SourceIdentity"))
            ? null
            : reader.GetGuid(reader.GetOrdinal("SourceIdentity"));

        if ((sourceKind is null) != (sourceIdentity is null))
            throw new InvalidOperationException(
                "Persisted WorkItem source state is invalid.");

        var source = sourceKind is null
            ? null
            : new ResourceReference(sourceKind.Value, sourceIdentity!.Value);

        var lifecycleStatus = (ResourceLifecycleStatus)reader.GetInt32(
            reader.GetOrdinal("LifecycleStatus"));
        var status = (WorkItemStatus)reader.GetInt32(
            reader.GetOrdinal("Status"));

        if (!Enum.IsDefined(lifecycleStatus) ||
            !Enum.IsDefined(status))
        {
            throw new InvalidOperationException(
                "Persisted WorkItem lifecycle or status is invalid.");
        }

        var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(
            reader.GetString(reader.GetOrdinal("MetadataJson")),
            JsonOptions) ?? throw new InvalidOperationException(
                "Persisted WorkItem metadata is invalid.");

        WorkItemAttachmentMetadata? attachment = null;

        if (!reader.IsDBNull(reader.GetOrdinal("AttachmentFileName")))
        {
            attachment = new WorkItemAttachmentMetadata(
                reader.GetString(reader.GetOrdinal("AttachmentFileName")),
                reader.GetString(reader.GetOrdinal("AttachmentMediaType")),
                reader.GetInt64(reader.GetOrdinal("AttachmentContentLength")),
                reader.GetString(reader.GetOrdinal("AttachmentSha256")));
        }

        var resource = new ResourceEnvelope<WorkItemId>(
            ResourceKind.WorkItem,
            new WorkItemId(reader.GetGuid(reader.GetOrdinal("WorkItemId"))),
            new PrincipalId(reader.GetGuid(reader.GetOrdinal("OwnerPrincipalId"))),
            scope,
            new ResourceVersion(reader.GetInt64(reader.GetOrdinal("ResourceVersion"))),
            new ResourceProvenance(
                new PrincipalId(reader.GetGuid(reader.GetOrdinal("CreatedByPrincipalId"))),
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        reader.GetDateTime(reader.GetOrdinal("CreatedAtUtc")),
                        DateTimeKind.Utc)),
                new CorrelationId(reader.GetGuid(reader.GetOrdinal("CorrelationId"))),
                reader.IsDBNull(reader.GetOrdinal("CausationId"))
                    ? null
                    : new CausationId(
                        reader.GetGuid(reader.GetOrdinal("CausationId"))),
                source),
            new ResourceLifecycle(
                lifecycleStatus,
                new DateTimeOffset(
                    DateTime.SpecifyKind(
                        reader.GetDateTime(reader.GetOrdinal("LifecycleChangedAtUtc")),
                        DateTimeKind.Utc))),
            metadata);

        return WorkItem.Restore(resource, status, attachment);
    }

    private static EventAppendRequest CreateAppendRequest(
        WorkItem workItem,
        EventEnvelope envelope) =>
        new(
            new ResourceReference(
                ResourceKind.WorkItem,
                workItem.Id.Value),
            null,
            envelope,
            CreateSnapshot(workItem));

    private static EventSnapshot CreateSnapshot(WorkItem workItem)
    {
        var document = new WorkItemStateDocument(workItem);

        return new EventSnapshot(
            new ResourceReference(
                ResourceKind.WorkItem,
                workItem.Id.Value),
            workItem.Resource.Version,
            new EventPayloadVersion(1),
            JsonSerializer.SerializeToElement(document, JsonOptions));
    }

    private static EventEnvelope CreateEvent(
        string eventType,
        WorkItem workItem,
        WorkItemStatus? previousStatus,
        string? reason) =>
        new JsonEventSerializer().CreateEnvelope(
            EventId.New(),
            DateTimeOffset.UtcNow,
            new EventType(eventType),
            new EventPayloadVersion(1),
            workItem.Resource.Provenance.CorrelationId,
            null,
            new
            {
                workItemId = workItem.Id.Value,
                version = workItem.Resource.Version.Value,
                previousStatus,
                status = workItem.Status,
                reason,
                attachment = workItem.Attachment
            });

    private sealed class WorkItemStateDocument
    {
        public WorkItemStateDocument(WorkItem workItem)
        {
            WorkItemId = workItem.Id.Value;
            OwnerPrincipalId = workItem.Resource.Owner.Value;
            ScopeKind = (int)workItem.Resource.Scope.Kind;
            ScopeIdentity = workItem.Resource.Scope.Identity;
            ResourceVersion = workItem.Resource.Version.Value;
            CreatedByPrincipalId = workItem.Resource.Provenance.CreatedBy.Value;
            CreatedAtUtc = workItem.Resource.Provenance.CreatedAtUtc;
            CorrelationId = workItem.Resource.Provenance.CorrelationId.Value;
            CausationId = workItem.Resource.Provenance.CausationId?.Value;
            SourceKind = workItem.Resource.Provenance.Source?.Kind;
            SourceIdentity = workItem.Resource.Provenance.Source?.Identity;
            LifecycleStatus = (int)workItem.Resource.Lifecycle.Status;
            LifecycleChangedAtUtc = workItem.Resource.Lifecycle.ChangedAtUtc;
            Status = (int)workItem.Status;
            Metadata = new Dictionary<string, string>(
                workItem.Resource.Metadata,
                StringComparer.Ordinal);
            Attachment = workItem.Attachment;
        }

        public Guid WorkItemId { get; }
        public Guid OwnerPrincipalId { get; }
        public int ScopeKind { get; }
        public Guid? ScopeIdentity { get; }
        public long ResourceVersion { get; }
        public Guid CreatedByPrincipalId { get; }
        public DateTimeOffset CreatedAtUtc { get; }
        public Guid CorrelationId { get; }
        public Guid? CausationId { get; }
        public ResourceKind? SourceKind { get; }
        public Guid? SourceIdentity { get; }
        public int LifecycleStatus { get; }
        public DateTimeOffset LifecycleChangedAtUtc { get; }
        public int Status { get; }
        public Dictionary<string, string> Metadata { get; }
        public WorkItemAttachmentMetadata? Attachment { get; }
    }

    private static void AddWorkItemParameters(
        SqlCommand command,
        WorkItem workItem)
    {
        AddWorkItemStateParameters(command, workItem);

        command.Parameters.Add(
            GuidParameter("@WorkItemId", workItem.Id.Value));
    }

    private static void AddWorkItemStateParameters(
        SqlCommand command,
        WorkItem workItem)
    {
        var resource = workItem.Resource;

        command.Parameters.Add(
            GuidParameter("@OwnerPrincipalId", resource.Owner.Value));
        command.Parameters.Add(
            IntParameter("@ScopeKind", (int)resource.Scope.Kind));
        command.Parameters.Add(
            GuidParameter("@ScopeIdentity", resource.Scope.Identity));
        command.Parameters.Add(
            BigIntParameter("@ResourceVersion", resource.Version.Value));
        command.Parameters.Add(
            GuidParameter("@CreatedByPrincipalId", resource.Provenance.CreatedBy.Value));
        command.Parameters.Add(
            DateTimeParameter("@CreatedAtUtc", resource.Provenance.CreatedAtUtc));
        command.Parameters.Add(
            GuidParameter("@CorrelationId", resource.Provenance.CorrelationId.Value));
        command.Parameters.Add(
            GuidParameter(
                "@CausationId",
                resource.Provenance.CausationId?.Value));
        command.Parameters.Add(
            new SqlParameter("@SourceKind", SqlDbType.Int)
            {
                Value = resource.Provenance.Source is null
                    ? DBNull.Value
                    : (object)(int)resource.Provenance.Source.Value.Kind
            });
        command.Parameters.Add(
            GuidParameter(
                "@SourceIdentity",
                resource.Provenance.Source?.Identity));
        command.Parameters.Add(
            IntParameter("@LifecycleStatus", (int)resource.Lifecycle.Status));
        command.Parameters.Add(
            DateTimeParameter(
                "@LifecycleChangedAtUtc",
                resource.Lifecycle.ChangedAtUtc));
        command.Parameters.Add(
            IntParameter("@Status", (int)workItem.Status));
        command.Parameters.Add(
            TextParameter(
                "@MetadataJson",
                -1,
                JsonSerializer.Serialize(
                    resource.Metadata,
                    JsonOptions)));

        command.Parameters.Add(
            TextParameter(
                "@AttachmentFileName",
                260,
                workItem.Attachment?.FileName));
        command.Parameters.Add(
            TextParameter(
                "@AttachmentMediaType",
                200,
                workItem.Attachment?.MediaType));
        command.Parameters.Add(
            BigIntNullableParameter(
                "@AttachmentContentLength",
                workItem.Attachment?.ContentLength));
        command.Parameters.Add(
            TextParameter(
                "@AttachmentSha256",
                64,
                workItem.Attachment?.Sha256));
    }

    private static void AddAccessParameters(
        SqlCommand command,
        ResourceAccessContext accessContext)
    {
        command.Parameters.Add(
            GuidParameter(
                "@PrincipalId",
                accessContext.PrincipalId!.Value.Value));
        command.Parameters.Add(
            GuidParameter("@TenantId", accessContext.TenantId?.Value));
        command.Parameters.Add(
            GuidParameter("@UserId", accessContext.UserId?.Value));
        command.Parameters.Add(
            GuidParameter("@WorkspaceId", accessContext.WorkspaceId?.Value));
        command.Parameters.Add(
            GuidParameter("@AgentId", accessContext.AgentId?.Value));
        command.Parameters.Add(
            GuidParameter("@RuntimeId", accessContext.RuntimeId?.Value));
        command.Parameters.Add(
            GuidParameter("@ExecutionId", accessContext.ExecutionId?.Value));
        command.Parameters.Add(
            IntParameter("@GlobalScope", (int)ResourceScopeKind.Global));
        command.Parameters.Add(
            IntParameter("@TenantScope", (int)ResourceScopeKind.Tenant));
        command.Parameters.Add(
            IntParameter("@UserScope", (int)ResourceScopeKind.User));
        command.Parameters.Add(
            IntParameter("@WorkspaceScope", (int)ResourceScopeKind.Workspace));
        command.Parameters.Add(
            IntParameter("@AgentScope", (int)ResourceScopeKind.Agent));
        command.Parameters.Add(
            IntParameter("@RuntimeScope", (int)ResourceScopeKind.Runtime));
        command.Parameters.Add(
            IntParameter("@ExecutionScope", (int)ResourceScopeKind.Execution));
    }

    private const string ScopeAccessPredicate = """
        (
            [ScopeKind] = @GlobalScope
            OR ([ScopeKind] = @TenantScope
                AND @TenantId IS NOT NULL
                AND [ScopeIdentity] = @TenantId)
            OR ([ScopeKind] = @UserScope
                AND @UserId IS NOT NULL
                AND [ScopeIdentity] = @UserId)
            OR ([ScopeKind] = @WorkspaceScope
                AND @WorkspaceId IS NOT NULL
                AND [ScopeIdentity] = @WorkspaceId)
            OR ([ScopeKind] = @AgentScope
                AND @AgentId IS NOT NULL
                AND [ScopeIdentity] = @AgentId)
            OR ([ScopeKind] = @RuntimeScope
                AND @RuntimeId IS NOT NULL
                AND [ScopeIdentity] = @RuntimeId)
            OR ([ScopeKind] = @ExecutionScope
                AND @ExecutionId IS NOT NULL
                AND [ScopeIdentity] = @ExecutionId)
        )
        """;

    private static ResourceScope CreateScope(ResourceAccessContext context)
    {
        if (context.WorkspaceId is not null)
            return ResourceScope.Workspace(context.WorkspaceId.Value);

        if (context.TenantId is not null)
            return ResourceScope.Tenant(context.TenantId.Value);

        return ResourceScope.Global();
    }

    private static Error? ValidateAccess(
        ResourceEnvelope<WorkItemId> resource,
        ResourceAccessContext accessContext)
    {
        if (resource.Owner != accessContext.PrincipalId)
        {
            return new Error(
                "hive.resource.owner-forbidden",
                ErrorCategory.Forbidden,
                "The current principal does not own the WorkItem.");
        }

        return resource.Scope.Matches(accessContext)
            ? null
            : new Error(
                "hive.resource.scope-forbidden",
                ErrorCategory.Forbidden,
                "The current access context is outside the WorkItem scope.");
    }

    private static void ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        ArgumentNullException.ThrowIfNull(accessContext);

        if (accessContext.PrincipalId is null ||
            accessContext.DeploymentId is null)
        {
            throw new InvalidOperationException(
                "A deployment and principal are required for WorkItem access.");
        }
    }

    private async Task<Result<T>> ExecuteInTransactionAsync<T>(
        string resourceName,
        CancellationToken cancellationToken,
        Func<SqlConnection, SqlTransaction, Task<Result<T>>> operation)
    {
        try
        {
            await using var connection =
                await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken).ConfigureAwait(false);

            var result = await operation(
                connection,
                transaction).ConfigureAwait(false);

            if (result.IsSuccess)
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            else
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Result<T>.Failure(
                Error.Conflict(
                    "hive.work-item.duplicate",
                    "The WorkItem or attachment already exists."));
        }
        catch (ConcurrencyException)
        {
            return Result<T>.Failure(
                Error.Concurrency(
                    "hive.work-item.concurrency",
                    "The WorkItem changed before the operation completed."));
        }
        catch (SqlException exception)
        {
            return Result<T>.Failure(ToSqlError(exception));
        }
        catch (Exception exception)
        {
            return Result<T>.Failure(ToInvalidStateError(exception));
        }
    }

    private async Task<SqlConnection> OpenConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_options.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private SqlCommand CreateCommand(
        SqlConnection connection,
        string commandText,
        SqlTransaction? transaction = null) =>
        new(commandText, connection, transaction)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

    private static SqlParameter GuidParameter(
        string name,
        Guid? value) =>
        new(name, SqlDbType.UniqueIdentifier)
        {
            Value = (object?)value ?? DBNull.Value
        };

    private static SqlParameter BigIntParameter(
        string name,
        long value) =>
        new(name, SqlDbType.BigInt)
        {
            Value = value
        };

    private static SqlParameter BigIntNullableParameter(
        string name,
        long? value) =>
        new(name, SqlDbType.BigInt)
        {
            Value = (object?)value ?? DBNull.Value
        };

    private static SqlParameter IntParameter(
        string name,
        int value) =>
        new(name, SqlDbType.Int)
        {
            Value = value
        };

    private static SqlParameter DateTimeParameter(
        string name,
        DateTimeOffset value) =>
        new(name, SqlDbType.DateTime2)
        {
            Value = value.UtcDateTime
        };

    private static SqlParameter TextParameter(
        string name,
        int size,
        object? value) =>
        new(name, SqlDbType.NVarChar, size)
        {
            Value = value ?? DBNull.Value
        };

    private static Error NotFound(
        string code,
        string message) =>
        Error.NotFound(code, message);

    private static Error ToSqlError(SqlException exception) =>
        new(
            "hive.persistence.work-item.sql-failure",
            ErrorCategory.External,
            $"SQL Server operation for the WorkItem failed: {exception.Message}");

    private static Error ToInvalidStateError(Exception exception) =>
        new(
            "hive.persistence.work-item.invalid-state",
            ErrorCategory.Internal,
            $"Persisted WorkItem state could not be read or validated: {exception.Message}");

    private sealed class ConcurrencyException : Exception
    {
    }
}
