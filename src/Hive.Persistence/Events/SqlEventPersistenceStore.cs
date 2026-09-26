using System.Data;
using System.Text.Json;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

public sealed class SqlEventPersistenceStore : IEventPersistenceStore, IEventOutboxPollerStore
{
    private readonly HiveDatabaseOptions _options;
    private readonly JsonEventSerializer _serializer;

    public SqlEventPersistenceStore(
        HiveDatabaseOptions options,
        JsonEventSerializer? serializer = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _serializer = serializer ?? new JsonEventSerializer();
    }

    public async Task<Result<EventAppendResult>> AppendAsync(
        EventAppendRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var transaction =
                (SqlTransaction)await connection.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken).ConfigureAwait(false);

            var result = await AppendInTransactionAsync(
                connection,
                transaction,
                request,
                cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception) when (IsConstraintConflict(exception))
        {
            return Result<EventAppendResult>.Failure(
                Error.Conflict(
                    "hive.event.duplicate",
                    "The event could not be persisted because a unique event or stream-version constraint was violated."));
        }
        catch (SqlException exception)
        {
            return Result<EventAppendResult>.Failure(
                HivePersistenceError.External("hive.event.sql", "The durable event operation failed at the SQL Server boundary.", exception));
        }
        catch (EventSerializationException exception)
        {
            return Result<EventAppendResult>.Failure(exception.Error);
        }
        catch (Exception exception)
        {
            return Result<EventAppendResult>.Failure(
                HivePersistenceError.Internal("hive.event.persistence", "The durable event operation failed.", exception));
        }
    }

    public async Task<Result<IReadOnlyList<PersistedEvent>>> ReadEventsAsync(
        ResourceReference stream,
        ResourceVersion? afterVersion = null,
        CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);

        if (afterVersion is not null && afterVersion.Value.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(afterVersion));

        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                """
                SELECT
                    [StreamVersion],
                    [EventId],
                    [OccurredAtUtc],
                    [EventType],
                    [PayloadSchemaVersion],
                    [CorrelationId],
                    [CausationId],
                    [PayloadJson]
                FROM [dbo].[HiveEventLog]
                WHERE [StreamKind] = @StreamKind
                  AND [StreamIdentity] = @StreamIdentity
                  AND [StreamVersion] > @AfterVersion
                ORDER BY [StreamVersion];
                """);

            AddStreamParameters(command, stream);
            command.Parameters.Add(
                BigIntParameter(
                    "@AfterVersion",
                    afterVersion?.Value ?? 0));

            var events = new List<PersistedEvent>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var streamVersion = new ResourceVersion(
                    reader.GetInt64(reader.GetOrdinal("StreamVersion")));

                var envelope = ReadEnvelope(reader);

                events.Add(
                    new PersistedEvent(
                        stream,
                        streamVersion,
                        envelope));
            }

            return Result<IReadOnlyList<PersistedEvent>>.Success(events);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EventSerializationException exception)
        {
            return Result<IReadOnlyList<PersistedEvent>>.Failure(exception.Error);
        }
        catch (SqlException exception)
        {
            return Result<IReadOnlyList<PersistedEvent>>.Failure(
                HivePersistenceError.External("hive.event.sql", "The durable event read failed at the SQL Server boundary.", exception));
        }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<PersistedEvent>>.Failure(
                HivePersistenceError.Internal("hive.event.read", "The durable event read failed.", exception));
        }
    }

    public async Task<Result<EventSnapshot?>> GetSnapshotAsync(
        ResourceReference stream,
        CancellationToken cancellationToken = default)
    {
        ValidateStream(stream);

        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                """
                SELECT
                    [SnapshotVersion],
                    [PayloadSchemaVersion],
                    [StateJson]
                FROM [dbo].[HiveEventSnapshots]
                WHERE [StreamKind] = @StreamKind
                  AND [StreamIdentity] = @StreamIdentity;
                """);

            AddStreamParameters(command, stream);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return Result<EventSnapshot?>.Success(null);

            var version = new ResourceVersion(
                reader.GetInt64(reader.GetOrdinal("SnapshotVersion")));
            var payloadSchemaVersion = new EventPayloadVersion(
                reader.GetInt32(reader.GetOrdinal("PayloadSchemaVersion")));
            var stateJson = reader.GetString(reader.GetOrdinal("StateJson"));

            using var document = JsonDocument.Parse(stateJson);

            return Result<EventSnapshot?>.Success(
                new EventSnapshot(
                    stream,
                    version,
                    payloadSchemaVersion,
                    document.RootElement));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return Result<EventSnapshot?>.Failure(
                HivePersistenceError.Serialization(
                    "hive.event.snapshot-invalid",
                    "The stored snapshot JSON is invalid.",
                    exception));
        }
        catch (SqlException exception)
        {
            return Result<EventSnapshot?>.Failure(
                HivePersistenceError.External("hive.event.sql", "The snapshot read failed at the SQL Server boundary.", exception));
        }
        catch (Exception exception)
        {
            return Result<EventSnapshot?>.Failure(
                HivePersistenceError.Internal("hive.event.snapshot-read", "The snapshot read failed.", exception));
        }
    }

    public async Task<Result<IReadOnlyList<EventSnapshot>>> ListSnapshotsAsync(
        ResourceKind streamKind,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(streamKind))
            throw new ArgumentOutOfRangeException(nameof(streamKind));

        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                """
                SELECT
                    [StreamKind],
                    [StreamIdentity],
                    [SnapshotVersion],
                    [PayloadSchemaVersion],
                    [StateJson]
                FROM [dbo].[HiveEventSnapshots]
                WHERE [StreamKind] = @StreamKind
                ORDER BY [StreamIdentity];
                """);

            command.Parameters.Add(
                IntParameter("@StreamKind", (int)streamKind));

            var snapshots = new List<EventSnapshot>();

            await using var reader = await command.ExecuteReaderAsync(
                cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var stream = ReadStreamReference(reader);
                var version = new ResourceVersion(
                    reader.GetInt64(reader.GetOrdinal("SnapshotVersion")));
                var payloadSchemaVersion = new EventPayloadVersion(
                    reader.GetInt32(reader.GetOrdinal("PayloadSchemaVersion")));

                using var document = JsonDocument.Parse(
                    reader.GetString(reader.GetOrdinal("StateJson")));

                snapshots.Add(
                    new EventSnapshot(
                        stream,
                        version,
                        payloadSchemaVersion,
                        document.RootElement));
            }

            return Result<IReadOnlyList<EventSnapshot>>.Success(snapshots);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            return Result<IReadOnlyList<EventSnapshot>>.Failure(
                HivePersistenceError.Serialization(
                    "hive.event.snapshot-invalid",
                    "The stored snapshot JSON is invalid.",
                    exception));
        }
        catch (SqlException exception)
        {
            return Result<IReadOnlyList<EventSnapshot>>.Failure(
                HivePersistenceError.External("hive.event.sql", "The snapshot listing failed at the SQL Server boundary.", exception));
        }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<EventSnapshot>>.Failure(
                HivePersistenceError.Internal("hive.event.snapshot-list", "The snapshot listing failed.", exception));
        }
    }

    public async Task<Result<EventOutboxEntry?>> GetOutboxAsync(
        EventId eventId,
        CancellationToken cancellationToken = default)
    {
        if (eventId == default)
            throw new ArgumentException("Event identity is required.", nameof(eventId));

        try
        {
            await using var connection = await OpenConnectionAsync(
                cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                """
                SELECT
                    [StreamKind],
                    [StreamIdentity],
                    [StreamVersion],
                    [EventId],
                    [OccurredAtUtc],
                    [EventType],
                    [PayloadSchemaVersion],
                    [CorrelationId],
                    [CausationId],
                    [PayloadJson]
                FROM [dbo].[HiveEventOutbox]
                WHERE [EventId] = @EventId;
                """);

            command.Parameters.Add(GuidParameter("@EventId", eventId.Value));

            await using var reader = await command.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                return Result<EventOutboxEntry?>.Success(null);

            var stream = ReadStreamReference(reader);
            var envelope = ReadEnvelope(reader);

            return Result<EventOutboxEntry?>.Success(
                new EventOutboxEntry(
                    stream,
                    new ResourceVersion(
                        reader.GetInt64(reader.GetOrdinal("StreamVersion"))),
                    envelope));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EventSerializationException exception)
        {
            return Result<EventOutboxEntry?>.Failure(exception.Error);
        }
        catch (SqlException exception)
        {
            return Result<EventOutboxEntry?>.Failure(
                HivePersistenceError.External("hive.event.sql", "The outbox read failed at the SQL Server boundary.", exception));
        }
        catch (Exception exception)
        {
            return Result<EventOutboxEntry?>.Failure(
                HivePersistenceError.Internal("hive.event.outbox-read", "The outbox read failed.", exception));
        }
    }

    public async Task<Result<EventOutboxWorkItem?>> ClaimNextOutboxAsync(
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        if (leaseDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));

        var leaseId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(leaseDuration);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false);

            await using var command = CreateCommand(
                connection,
                """
                ;WITH [NextOutbox] AS
                (
                    SELECT TOP (1) [EventId]
                    FROM [dbo].[HiveEventOutbox] WITH (UPDLOCK, READPAST, READCOMMITTEDLOCK, ROWLOCK)
                    WHERE [LeaseId] IS NULL OR [LeaseExpiresAtUtc] <= @NowUtc
                    ORDER BY [CreatedAtUtc], [EventId]
                )
                UPDATE [outbox]
                SET [LeaseId] = @LeaseId,
                    [LeaseExpiresAtUtc] = @LeaseExpiresAtUtc,
                    [AttemptCount] = [AttemptCount] + 1
                OUTPUT
                    inserted.[StreamKind], inserted.[StreamIdentity], inserted.[StreamVersion],
                    inserted.[EventId], inserted.[OccurredAtUtc], inserted.[EventType],
                    inserted.[PayloadSchemaVersion], inserted.[CorrelationId], inserted.[CausationId],
                    inserted.[PayloadJson], inserted.[AttemptCount]
                FROM [dbo].[HiveEventOutbox] AS [outbox]
                INNER JOIN [NextOutbox] ON [NextOutbox].[EventId] = [outbox].[EventId];
                """,
                transaction);

            command.Parameters.Add(DateTimeParameter("@NowUtc", now));
            command.Parameters.Add(GuidParameter("@LeaseId", leaseId));
            command.Parameters.Add(DateTimeParameter("@LeaseExpiresAtUtc", expiresAt));

            EventOutboxWorkItem? workItem = null;
            await using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var stream = ReadStreamReference(reader);
                    var envelope = ReadEnvelope(reader);
                    var entry = new EventOutboxEntry(
                        stream,
                        new ResourceVersion(reader.GetInt64(reader.GetOrdinal("StreamVersion"))),
                        envelope);
                    workItem = new EventOutboxWorkItem(
                        entry,
                        leaseId,
                        reader.GetInt32(reader.GetOrdinal("AttemptCount")));
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return Result<EventOutboxWorkItem?>.Success(workItem);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (EventSerializationException exception)
        {
            return Result<EventOutboxWorkItem?>.Failure(exception.Error);
        }
        catch (SqlException exception)
        {
            return Result<EventOutboxWorkItem?>.Failure(HivePersistenceError.External("hive.outbox.claim-sql", "The outbox claim failed at the SQL Server boundary.", exception));
        }
        catch (Exception exception)
        {
            return Result<EventOutboxWorkItem?>.Failure(HivePersistenceError.Internal("hive.outbox.claim", "The outbox claim failed.", exception));
        }
    }

    public async Task<Result> CompleteOutboxAsync(
        EventOutboxWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);

        try
        {
            await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            await using var command = CreateCommand(
                connection,
                """
                DELETE FROM [dbo].[HiveEventOutbox]
                WHERE [EventId] = @EventId
                  AND [LeaseId] = @LeaseId
                  AND [LeaseExpiresAtUtc] > @NowUtc;
                """);
            command.Parameters.Add(GuidParameter("@EventId", workItem.Entry.Envelope.EventId.Value));
            command.Parameters.Add(GuidParameter("@LeaseId", workItem.LeaseId));
            command.Parameters.Add(
                DateTimeParameter(
                    "@NowUtc",
                    DateTimeOffset.UtcNow));

            var affected = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return affected == 1
                ? Result.Success()
                : Result.Failure(Error.Concurrency(
                    "hive.outbox.lease-lost",
                    "The outbox lease was lost before the delivery could be acknowledged."));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SqlException exception)
        {
            return Result.Failure(HivePersistenceError.External("hive.outbox.complete-sql", "The outbox completion failed at the SQL Server boundary.", exception));
        }
        catch (Exception exception)
        {
            return Result.Failure(HivePersistenceError.Internal("hive.outbox.complete", "The outbox completion failed.", exception));
        }
    }

    internal async Task<Result<EventAppendResult>> AppendInTransactionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EventAppendRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(request);
        ValidateStream(request.Stream);

        var currentVersion = await ReadCurrentVersionAsync(
            connection,
            transaction,
            request.Stream,
            cancellationToken).ConfigureAwait(false);

        var expectedVersion = request.ExpectedVersion?.Value ?? 0;

        if (currentVersion != expectedVersion)
        {
            return Result<EventAppendResult>.Failure(
                Error.Concurrency(
                    "hive.event.stream-version",
                    $"The event stream is at version {currentVersion}, but version {expectedVersion} was expected."));
        }

        await InsertEventAsync(
            connection,
            transaction,
            request,
            cancellationToken).ConfigureAwait(false);

        if (request.Snapshot is not null)
        {
            await UpsertSnapshotAsync(
                connection,
                transaction,
                request.Snapshot,
                currentVersion,
                cancellationToken).ConfigureAwait(false);
        }

        await InsertOutboxAsync(
            connection,
            transaction,
            request,
            cancellationToken).ConfigureAwait(false);

        var persistedEvent = new PersistedEvent(
            request.Stream,
            request.StreamVersion,
            request.Envelope);

        var outbox = new EventOutboxEntry(
            request.Stream,
            request.StreamVersion,
            request.Envelope);

        return Result<EventAppendResult>.Success(
            new EventAppendResult(
                persistedEvent,
                request.Snapshot,
                outbox));
    }

    private async Task<long> ReadCurrentVersionAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        ResourceReference stream,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            SELECT ISNULL(MAX([StreamVersion]), 0)
            FROM [dbo].[HiveEventLog] WITH (UPDLOCK, HOLDLOCK)
            WHERE [StreamKind] = @StreamKind
              AND [StreamIdentity] = @StreamIdentity;
            """,
            transaction);

        AddStreamParameters(command, stream);

        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken)
                .ConfigureAwait(false));
    }

    private async Task InsertEventAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EventAppendRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            INSERT INTO [dbo].[HiveEventLog]
            (
                [EventId],
                [StreamKind],
                [StreamIdentity],
                [StreamVersion],
                [OccurredAtUtc],
                [EventType],
                [PayloadSchemaVersion],
                [CorrelationId],
                [CausationId],
                [PayloadJson]
            )
            VALUES
            (
                @EventId,
                @StreamKind,
                @StreamIdentity,
                @StreamVersion,
                @OccurredAtUtc,
                @EventType,
                @PayloadSchemaVersion,
                @CorrelationId,
                @CausationId,
                @PayloadJson
            );
            """,
            transaction);

        AddEnvelopeParameters(command, request);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task UpsertSnapshotAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EventSnapshot snapshot,
        long previousVersion,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            IF EXISTS
            (
                SELECT 1
                FROM [dbo].[HiveEventSnapshots] WITH (UPDLOCK, HOLDLOCK)
                WHERE [StreamKind] = @StreamKind
                  AND [StreamIdentity] = @StreamIdentity
            )
            BEGIN
                UPDATE [dbo].[HiveEventSnapshots]
                SET [SnapshotVersion] = @SnapshotVersion,
                    [PayloadSchemaVersion] = @PayloadSchemaVersion,
                    [StateJson] = @StateJson,
                    [UpdatedAtUtc] = SYSUTCDATETIME()
                WHERE [StreamKind] = @StreamKind
                  AND [StreamIdentity] = @StreamIdentity
                  AND [SnapshotVersion] <= @PreviousVersion;
            END
            ELSE
            BEGIN
                INSERT INTO [dbo].[HiveEventSnapshots]
                (
                    [StreamKind],
                    [StreamIdentity],
                    [SnapshotVersion],
                    [PayloadSchemaVersion],
                    [StateJson],
                    [UpdatedAtUtc]
                )
                VALUES
                (
                    @StreamKind,
                    @StreamIdentity,
                    @SnapshotVersion,
                    @PayloadSchemaVersion,
                    @StateJson,
                    SYSUTCDATETIME()
                );
            END;
            """,
            transaction);

        AddSnapshotParameters(command, snapshot);
        command.Parameters.Add(
            BigIntParameter(
                "@PreviousVersion",
                previousVersion));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task InsertOutboxAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        EventAppendRequest request,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(
            connection,
            """
            INSERT INTO [dbo].[HiveEventOutbox]
            (
                [EventId],
                [StreamKind],
                [StreamIdentity],
                [StreamVersion],
                [OccurredAtUtc],
                [EventType],
                [PayloadSchemaVersion],
                [CorrelationId],
                [CausationId],
                [PayloadJson],
                [CreatedAtUtc]
            )
            VALUES
            (
                @EventId,
                @StreamKind,
                @StreamIdentity,
                @StreamVersion,
                @OccurredAtUtc,
                @EventType,
                @PayloadSchemaVersion,
                @CorrelationId,
                @CausationId,
                @PayloadJson,
                SYSUTCDATETIME()
            );
            """,
            transaction);

        AddEnvelopeParameters(command, request);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private EventEnvelope ReadEnvelope(SqlDataReader reader)
    {
        var eventId = new EventId(
            reader.GetGuid(reader.GetOrdinal("EventId")));
        var occurredAtUtc = new DateTimeOffset(
            DateTime.SpecifyKind(
                reader.GetDateTime(reader.GetOrdinal("OccurredAtUtc")),
                DateTimeKind.Utc));
        var eventType = new EventType(
            reader.GetString(reader.GetOrdinal("EventType")));
        var payloadSchemaVersion = new EventPayloadVersion(
            reader.GetInt32(reader.GetOrdinal("PayloadSchemaVersion")));
        var correlationId = new CorrelationId(
            reader.GetGuid(reader.GetOrdinal("CorrelationId")));
        CausationId? causationId = reader.IsDBNull(reader.GetOrdinal("CausationId"))
            ? null
            : new CausationId(
                reader.GetGuid(reader.GetOrdinal("CausationId")));

        try
        {
            using var document = JsonDocument.Parse(
                reader.GetString(reader.GetOrdinal("PayloadJson")));

            return EventEnvelope.Create(
                eventId,
                occurredAtUtc,
                eventType,
                payloadSchemaVersion,
                correlationId,
                causationId,
                document.RootElement);
        }
        catch (JsonException exception)
        {
            throw new EventSerializationException(
                new Error(
                    "event.payload.invalid",
                    ErrorCategory.Serialization,
                    "Stored event payload JSON is invalid."),
                exception);
        }
    }

    private static ResourceReference ReadStreamReference(SqlDataReader reader) =>
        new(
            (ResourceKind)reader.GetInt32(reader.GetOrdinal("StreamKind")),
            reader.GetGuid(reader.GetOrdinal("StreamIdentity")));

    private void AddEnvelopeParameters(
        SqlCommand command,
        EventAppendRequest request)
    {
        command.Parameters.Add(
            GuidParameter("@EventId", request.Envelope.EventId.Value));
        AddStreamParameters(command, request.Stream);
        command.Parameters.Add(
            BigIntParameter(
                "@StreamVersion",
                request.StreamVersion.Value));
        command.Parameters.Add(
            DateTimeParameter(
                "@OccurredAtUtc",
                request.Envelope.OccurredAtUtc));
        command.Parameters.Add(
            new SqlParameter(
                "@EventType",
                SqlDbType.NVarChar,
                200)
            {
                Value = request.Envelope.EventType.Value
            });
        command.Parameters.Add(
            IntParameter(
                "@PayloadSchemaVersion",
                request.Envelope.PayloadSchemaVersion.Value));
        command.Parameters.Add(
            GuidParameter(
                "@CorrelationId",
                request.Envelope.CorrelationId.Value));
        command.Parameters.Add(
            request.Envelope.CausationId is null
                ? new SqlParameter("@CausationId", SqlDbType.UniqueIdentifier)
                {
                    Value = DBNull.Value
                }
                : GuidParameter(
                    "@CausationId",
                    request.Envelope.CausationId.Value.Value));
        command.Parameters.Add(
            new SqlParameter(
                "@PayloadJson",
                SqlDbType.NVarChar,
                -1)
            {
                Value = JsonSerializer.Serialize(
                    request.Envelope.Payload,
                    _serializer.Options)
            });
    }

    private static void AddSnapshotParameters(
        SqlCommand command,
        EventSnapshot snapshot)
    {
        AddStreamParameters(command, snapshot.Stream);
        command.Parameters.Add(
            BigIntParameter(
                "@SnapshotVersion",
                snapshot.Version.Value));
        command.Parameters.Add(
            IntParameter(
                "@PayloadSchemaVersion",
                snapshot.PayloadSchemaVersion.Value));
        command.Parameters.Add(
            new SqlParameter(
                "@StateJson",
                SqlDbType.NVarChar,
                -1)
            {
                Value = snapshot.State.GetRawText()
            });
    }

    private static void AddStreamParameters(
        SqlCommand command,
        ResourceReference stream)
    {
        command.Parameters.Add(
            IntParameter(
                "@StreamKind",
                (int)stream.Kind));
        command.Parameters.Add(
            GuidParameter(
                "@StreamIdentity",
                stream.Identity));
    }

    private SqlCommand CreateCommand(
        SqlConnection connection,
        string commandText,
        SqlTransaction? transaction = null) =>
        new(commandText, connection, transaction)
        {
            CommandTimeout = _options.CommandTimeoutSeconds
        };

    private static SqlParameter GuidParameter(string name, Guid value) =>
        new(name, SqlDbType.UniqueIdentifier) { Value = value };

    private static SqlParameter BigIntParameter(string name, long value) =>
        new(name, SqlDbType.BigInt) { Value = value };

    private static SqlParameter IntParameter(string name, int value) =>
        new(name, SqlDbType.Int) { Value = value };

    private static SqlParameter DateTimeParameter(
        string name,
        DateTimeOffset value) =>
        new(name, SqlDbType.DateTime2)
        {
            Value = value.UtcDateTime
        };

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

    private static void ValidateStream(ResourceReference stream)
    {
        if (!Enum.IsDefined(stream.Kind))
            throw new ArgumentOutOfRangeException(nameof(stream), "Event stream resource kind is invalid.");

        if (stream.Identity == Guid.Empty)
            throw new ArgumentException("Event stream identity is required.", nameof(stream));
    }

    private static bool IsConstraintConflict(SqlException exception) =>
        exception.Number is 2601 or 2627;
}
