using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlWorkItemResourceStore : SqlResourceStoreBase, IWorkItemResourceStore
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

    private readonly SqlEventPersistenceStore _eventStore;

    protected override IsolationLevel TransactionIsolationLevel =>
        IsolationLevel.Serializable;

    public SqlWorkItemResourceStore(HiveDatabaseOptions options)
        : base(options)
    {
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
                    submission.Content.Length,
                    Convert.ToHexString(
                        SHA256.HashData(submission.Content.Span))
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
            var accessError = ValidateAccess(workItem.Resource, accessContext, "work item");

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
            return Result<WorkItem>.Failure(ToSqlError("work item", exception));
        }
        catch (Exception exception)
        {
            return Result<WorkItem>.Failure(ToInvalidStateError("work item", exception));
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
            return Result<IReadOnlyList<WorkItem>>.Failure(ToSqlError("work item", exception));
        }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<WorkItem>>.Failure(ToInvalidStateError("work item", exception));
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
                NotFound(
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
            return Result<WorkItemAttachmentContent>.Failure(ToSqlError("work item", exception));
        }
        catch (Exception exception)
        {
            return Result<WorkItemAttachmentContent>.Failure(ToInvalidStateError("work item", exception));
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

                var accessError = ValidateAccess(current.Resource, accessContext, "work item");

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
            SqlParameter("@NewVersion", SqlDbType.BigInt, updated.Resource.Version.Value));
        command.Parameters.Add(
            GuidParameter("@WorkItemId", updated.Id.Value));
        command.Parameters.Add(
            SqlParameter("@ExpectedVersion", SqlDbType.BigInt, current.Resource.Version.Value));

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
            SqlParameter("@FileName", SqlDbType.NVarChar, 260, submission.FileName));
        command.Parameters.Add(
            SqlParameter("@MediaType", SqlDbType.NVarChar, 200, submission.MediaType));
        command.Parameters.Add(
            SqlParameter("@ContentLength", SqlDbType.BigInt, attachment.ContentLength));
        command.Parameters.Add(
            SqlParameter("@Sha256", SqlDbType.NVarChar, 64, attachment.Sha256));
        command.Parameters.Add(
            new SqlParameter("@Content", SqlDbType.VarBinary, -1)
            {
                Value = submission.Content.ToArray()
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

        Guid? scopeIdentity = reader.IsDBNull(reader.GetOrdinal("ScopeIdentity"))
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

        ResourceReference? source = sourceKind is null
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

        var metadata = DeserializeMetadata(
            reader.GetString(reader.GetOrdinal("MetadataJson")));

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
            JsonSerializer.SerializeToElement(document));
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

        AddResourceParameters(command, resource);

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
            IntParameter("@Status", (int)workItem.Status));

        command.Parameters.Add(
            SqlParameter("@AttachmentFileName", SqlDbType.NVarChar, 260, workItem.Attachment?.FileName));
        command.Parameters.Add(
            SqlParameter("@AttachmentMediaType", SqlDbType.NVarChar, 200, workItem.Attachment?.MediaType));
        command.Parameters.Add(
            SqlParameter("@AttachmentContentLength", SqlDbType.BigInt, workItem.Attachment?.ContentLength));
        command.Parameters.Add(
            SqlParameter("@AttachmentSha256", SqlDbType.NVarChar, 64, workItem.Attachment?.Sha256));
    }

    private static ResourceScope CreateScope(ResourceAccessContext context)
    {
        if (context.WorkspaceId is not null)
            return ResourceScope.Workspace(context.WorkspaceId.Value);

        if (context.TenantId is not null)
            return ResourceScope.Tenant(context.TenantId.Value);

        return ResourceScope.Global();
    }


}