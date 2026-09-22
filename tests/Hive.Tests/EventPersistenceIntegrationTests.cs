using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using System.Text.Json;
using Xunit;

namespace Hive.Tests;

public sealed class EventPersistenceIntegrationTests
{
    [Fact]
    public async Task Append_PersistsEventSnapshotAndOutboxAndCanReadThemBack()
    {
        var database = await PrepareDatabase("Hive_Test_EventAppend");
        var store = new SqlEventPersistenceStore(database.Options);
        var stream = new ResourceReference(
            ResourceKind.WorkItem,
            Guid.NewGuid());

        var firstEvent = CreateEvent(
            "workitem.created",
            new { status = "Created" },
            EventTestData.Timestamp);

        var firstAppend = await store.AppendAsync(
            new EventAppendRequest(
                stream,
                null,
                firstEvent,
                CreateSnapshot(stream, ResourceVersion.Initial, new { status = "Created" })));

        Assert.True(firstAppend.IsSuccess, firstAppend.Error?.Message);
        Assert.Equal(ResourceVersion.Initial, firstAppend.Value!.Event.StreamVersion);
        Assert.Equal(firstEvent.EventId, firstAppend.Value!.Outbox.Envelope.EventId);

        var events = await store.ReadEventsAsync(stream);
        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Single(events.Value!);
        Assert.Equal(firstEvent.EventId, events.Value![0].Envelope.EventId);
        Assert.Equal(firstEvent.EventType, events.Value[0].Envelope.EventType);
        Assert.Equal(firstEvent.PayloadSchemaVersion, events.Value[0].Envelope.PayloadSchemaVersion);
        Assert.Equal(1, events.Value[0].StreamVersion.Value);

        var snapshot = await store.GetSnapshotAsync(stream);
        Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
        Assert.Equal(1, snapshot.Value!.Version.Value);
        Assert.Equal("Created", snapshot.Value.State.GetProperty("status").GetString());

        var outbox = await store.GetOutboxAsync(firstEvent.EventId);
        Assert.True(outbox.IsSuccess, outbox.Error?.Message);
        Assert.NotNull(outbox.Value);
        Assert.Equal(stream, outbox.Value!.Stream);
        Assert.Equal(firstEvent.EventType, outbox.Value.Envelope.EventType);

        var secondEvent = CreateEvent(
            "workitem.running",
            new { status = "Running" },
            EventTestData.Timestamp.AddMinutes(1));

        var secondAppend = await store.AppendAsync(
            new EventAppendRequest(
                stream,
                firstAppend.Value!.Event.StreamVersion,
                secondEvent,
                CreateSnapshot(
                    stream,
                    new ResourceVersion(2),
                    new { status = "Running" })));

        Assert.True(secondAppend.IsSuccess, secondAppend.Error?.Message);

        var allEvents = await store.ReadEventsAsync(stream);
        Assert.True(allEvents.IsSuccess, allEvents.Error?.Message);
        Assert.Equal(2, allEvents.Value!.Count);
        Assert.Equal(1, allEvents.Value[0].StreamVersion.Value);
        Assert.Equal(2, allEvents.Value[1].StreamVersion.Value);

        var remaining = await store.ReadEventsAsync(
            stream,
            new ResourceVersion(1));

        Assert.True(remaining.IsSuccess, remaining.Error?.Message);
        Assert.Single(remaining.Value!);
        Assert.Equal(secondEvent.EventId, remaining.Value![0].Envelope.EventId);
    }

    [Fact]
    public async Task Append_RejectsStaleExpectedVersionWithoutWritingAnything()
    {
        var database = await PrepareDatabase("Hive_Test_EventStale");
        var store = new SqlEventPersistenceStore(database.Options);
        var stream = new ResourceReference(
            ResourceKind.Agent,
            Guid.NewGuid());

        var first = CreateEvent(
            "agent.created",
            new { generation = "Base" },
            EventTestData.Timestamp);

        var created = await store.AppendAsync(
            new EventAppendRequest(stream, null, first));

        Assert.True(created.IsSuccess, created.Error?.Message);

        var staleEvent = CreateEvent(
            "agent.updated",
            new { displayName = "Stale" },
            EventTestData.Timestamp.AddMinutes(1));

        var rejected = await store.AppendAsync(
            new EventAppendRequest(stream, null, staleEvent));

        Assert.True(rejected.IsFailure);
        Assert.Equal("hive.event.stream-version", rejected.Error?.Code);
        Assert.Equal(ErrorCategory.Concurrency, rejected.Error?.Category);

        var events = await store.ReadEventsAsync(stream);
        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Single(events.Value!);

        var outbox = await store.GetOutboxAsync(staleEvent.EventId);
        Assert.True(outbox.IsSuccess, outbox.Error?.Message);
        Assert.Null(outbox.Value);
    }

    [Fact]
    public async Task ConcurrentFirstAppends_AllowOnlyOneStreamVersion()
    {
        var database = await PrepareDatabase("Hive_Test_EventConcurrency");
        var store = new SqlEventPersistenceStore(database.Options);
        var stream = new ResourceReference(
            ResourceKind.Execution,
            Guid.NewGuid());

        var first = CreateEvent(
            "execution.started",
            new { state = "Running" },
            EventTestData.Timestamp);

        var second = CreateEvent(
            "execution.started",
            new { state = "Running" },
            EventTestData.Timestamp.AddTicks(1));

        var results = await Task.WhenAll(
            store.AppendAsync(
                new EventAppendRequest(stream, null, first)),
            store.AppendAsync(
                new EventAppendRequest(stream, null, second)));

        Assert.Equal(1, results.Count(result => result.IsSuccess));
        Assert.Equal(1, results.Count(result =>
            result.IsFailure &&
            result.Error?.Category == ErrorCategory.Concurrency));

        var events = await store.ReadEventsAsync(stream);
        Assert.True(events.IsSuccess, events.Error?.Message);
        Assert.Single(events.Value!);
    }

    [Fact]
    public async Task FailedTransaction_DoesNotLeaveEventSnapshotOrOutbox()
    {
        var database = await PrepareDatabase("Hive_Test_EventRollback");
        var store = new SqlEventPersistenceStore(database.Options);
        var stream = new ResourceReference(
            ResourceKind.WorkItem,
            Guid.NewGuid());
        var eventEnvelope = CreateEvent(
            "workitem.rollback-probe",
            new { status = "Probe" },
            EventTestData.Timestamp);

        await InstallRollbackTriggerAsync(database.Options);

        try
        {
            var result = await store.AppendAsync(
                new EventAppendRequest(
                    stream,
                    null,
                    eventEnvelope,
                    CreateSnapshot(
                        stream,
                        ResourceVersion.Initial,
                        new { status = "Probe" })));

            Assert.True(result.IsFailure);
            Assert.Equal(ErrorCategory.External, result.Error?.Category);

            var events = await store.ReadEventsAsync(stream);
            Assert.True(events.IsSuccess, events.Error?.Message);
            Assert.Empty(events.Value!);

            var snapshot = await store.GetSnapshotAsync(stream);
            Assert.True(snapshot.IsSuccess, snapshot.Error?.Message);
            Assert.Null(snapshot.Value);

            var outbox = await store.GetOutboxAsync(eventEnvelope.EventId);
            Assert.True(outbox.IsSuccess, outbox.Error?.Message);
            Assert.Null(outbox.Value);
        }
        finally
        {
            await DropRollbackTriggerAsync(database.Options);
        }
    }

    private static async Task<PersistenceTestDatabase> PrepareDatabase(string name)
    {
        var database = new PersistenceTestDatabase(name);
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options)
            .MigrateAsync();

        Assert.True(
            migration.IsSuccess,
            migration.Error is null
                ? "Migration failed without an error."
                : $"Migration failed: {migration.Error.Code} [{migration.Error.Category}] {migration.Error.Message}");

        return database;
    }

    private static EventEnvelope CreateEvent(
        string eventType,
        object payload,
        DateTimeOffset occurredAtUtc) =>
        new JsonEventSerializer().CreateEnvelope(
            EventId.New(),
            occurredAtUtc,
            new EventType(eventType),
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            payload);

    private static EventSnapshot CreateSnapshot(
        ResourceReference stream,
        ResourceVersion version,
        object state)
    {
        var json = JsonSerializer.SerializeToElement(state);

        return new EventSnapshot(
            stream,
            version,
            new EventPayloadVersion(1),
            json);
    }

    private static async Task InstallRollbackTriggerAsync(
        HiveDatabaseOptions options)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER [dbo].[TR_HiveEventLog_RollbackProbe]
            ON [dbo].[HiveEventLog]
            AFTER INSERT
            AS
            BEGIN
                THROW 51000, 'Intentional Hive event transaction rollback probe.', 1;
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropRollbackTriggerAsync(
        HiveDatabaseOptions options)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.TR_HiveEventLog_RollbackProbe', N'TR') IS NOT NULL
                DROP TRIGGER [dbo].[TR_HiveEventLog_RollbackProbe];
            """;
        await command.ExecuteNonQueryAsync();
    }
}
