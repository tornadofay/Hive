using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Hive.Tests;

public sealed class EventOutboxPollerIntegrationTests
{
    [Fact]
    public async Task ProcessNext_DeliversEventAndRemovesOutboxRow()
    {
        var database = await PrepareDatabase("Hive_Test_OutboxProcess");
        var store = new SqlEventPersistenceStore(database.Options);
        var eventEnvelope = CreateEvent("outbox.process");
        await AppendAsync(store, eventEnvelope);

        var handler = new RecordingHandler();
        var poller = new EventOutboxPoller(store, TimeSpan.FromSeconds(5));
        var result = await poller.ProcessNextAsync(handler);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(eventEnvelope.EventId, result.Value!.Envelope.EventId);
        Assert.Single(handler.EventIds);

        var remaining = await store.GetOutboxAsync(eventEnvelope.EventId);
        Assert.True(remaining.IsSuccess, remaining.Error?.Message);
        Assert.Null(remaining.Value);
    }

    [Fact]
    public async Task CompleteOutbox_ExpiredLeaseIsRejectedAndRowRemains()
    {
        var database = await PrepareDatabase("Hive_Test_OutboxExpiredCompletion");
        var store = new SqlEventPersistenceStore(database.Options);
        var eventEnvelope = CreateEvent("outbox.expired-completion");
        await AppendAsync(store, eventEnvelope);

        var claimed = await store.ClaimNextOutboxAsync(TimeSpan.FromMinutes(5));
        Assert.True(claimed.IsSuccess, claimed.Error?.Message);
        Assert.NotNull(claimed.Value);

        var workItem = claimed.Value!;
        await ExpireLeaseAsync(database.Options, eventEnvelope.EventId);

        var completed = await store.CompleteOutboxAsync(workItem);

        Assert.True(completed.IsFailure);
        Assert.Equal("hive.outbox.lease-lost", completed.Error!.Code);
        Assert.Equal(ErrorCategory.Concurrency, completed.Error.Category);

        var remaining = await store.GetOutboxAsync(eventEnvelope.EventId);
        Assert.True(remaining.IsSuccess, remaining.Error?.Message);
        Assert.NotNull(remaining.Value);
    }

    [Fact]
    public async Task ProcessNext_FailedDeliveryIsRetriedAfterLeaseExpiryWithoutDuplicatingSideEffect()
    {
        var database = await PrepareDatabase("Hive_Test_OutboxRetry");
        var store = new SqlEventPersistenceStore(database.Options);
        var eventEnvelope = CreateEvent("outbox.retry");
        await AppendAsync(store, eventEnvelope);

        var handler = new FailAfterSideEffectHandler();
        var poller = new EventOutboxPoller(store, TimeSpan.FromSeconds(5));
        var first = await poller.ProcessNextAsync(handler);

        Assert.True(first.IsFailure);
        Assert.Equal("test.outbox.delivery-failed", first.Error?.Code);
        Assert.Equal(1, handler.SideEffectCount);

        await ExpireLeaseAsync(database.Options, eventEnvelope.EventId);
        var second = await poller.ProcessNextAsync(handler);

        Assert.True(second.IsSuccess, second.Error?.Message);
        Assert.Equal(eventEnvelope.EventId, second.Value!.Envelope.EventId);
        Assert.Equal(1, handler.SideEffectCount);

        var remaining = await store.GetOutboxAsync(eventEnvelope.EventId);
        Assert.True(remaining.IsSuccess, remaining.Error?.Message);
        Assert.Null(remaining.Value);
    }

    [Fact]
    public async Task ProcessNext_CancellationAfterClaimLeavesLeaseForRecovery()
    {
        var database = await PrepareDatabase("Hive_Test_OutboxCancellation");
        var store = new SqlEventPersistenceStore(database.Options);
        var eventEnvelope = CreateEvent("outbox.cancellation");
        await AppendAsync(store, eventEnvelope);

        var poller = new EventOutboxPoller(store, TimeSpan.FromSeconds(5));
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            poller.ProcessNextAsync(new CancelingHandler(), CancellationToken.None));

        await ExpireLeaseAsync(database.Options, eventEnvelope.EventId);
        var handler = new RecordingHandler();
        var recovered = await poller.ProcessNextAsync(handler);

        Assert.True(recovered.IsSuccess, recovered.Error?.Message);
        Assert.Equal(eventEnvelope.EventId, recovered.Value!.Envelope.EventId);
        Assert.Single(handler.EventIds);
    }

    [Fact]
    public async Task ConcurrentPollers_OnlyOneClaimsTheSameOutboxRow()
    {
        var database = await PrepareDatabase("Hive_Test_OutboxConcurrency");
        var store = new SqlEventPersistenceStore(database.Options);
        var eventEnvelope = CreateEvent("outbox.concurrent");
        await AppendAsync(store, eventEnvelope);

        var handler = new RecordingHandler(TimeSpan.FromMilliseconds(100));
        var poller = new EventOutboxPoller(store, TimeSpan.FromSeconds(5));

        var results = await Task.WhenAll(
            poller.ProcessNextAsync(handler),
            poller.ProcessNextAsync(handler));

        Assert.Equal(1, results.Count(result => result.IsSuccess && result.Value is not null));
        Assert.Equal(1, results.Count(result => result.IsSuccess && result.Value is null));
        Assert.Single(handler.EventIds);
    }

    private static async Task<PersistenceTestDatabase> PrepareDatabase(string name)
    {
        var database = new PersistenceTestDatabase(name);
        database.Reset();
        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(
            migration.IsSuccess,
            migration.Error is null
                ? "Migration failed without an error."
                : $"Migration failed: {migration.Error.Code} [{migration.Error.Category}] {migration.Error.Message}");
        return database;
    }

    private static EventEnvelope CreateEvent(string eventType) =>
        new JsonEventSerializer().CreateEnvelope(
            EventId.New(),
            EventTestData.Timestamp,
            new EventType(eventType),
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new { message = eventType });

    private static async Task AppendAsync(SqlEventPersistenceStore store, EventEnvelope envelope)
    {
        var result = await store.AppendAsync(new EventAppendRequest(
            new ResourceReference(ResourceKind.WorkItem, Guid.NewGuid()),
            null,
            envelope));
        Assert.True(result.IsSuccess, result.Error?.Message);
    }

    private static async Task ExpireLeaseAsync(HiveDatabaseOptions options, EventId eventId)
    {
        await using var connection = new SqlConnection(options.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE [dbo].[HiveEventOutbox]
            SET [LeaseExpiresAtUtc] = '2000-01-01T00:00:00.0000000'
            WHERE [EventId] = @EventId;
            """;
        command.Parameters.AddWithValue("@EventId", eventId.Value);
        Assert.Equal(1, await command.ExecuteNonQueryAsync());
    }

    private sealed class RecordingHandler : IEventOutboxHandler
    {
        private readonly TimeSpan _delay;

        public RecordingHandler(TimeSpan delay = default) => _delay = delay;

        public List<EventId> EventIds { get; } = [];

        public async Task<Result> HandleAsync(EventOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_delay > TimeSpan.Zero)
                await Task.Delay(_delay, cancellationToken);
            EventIds.Add(entry.Envelope.EventId);
            return Result.Success();
        }
    }

    private sealed class FailAfterSideEffectHandler : IEventOutboxHandler
    {
        private readonly HashSet<EventId> _seen = [];
        private bool _failFirst = true;
        public int SideEffectCount { get; private set; }

        public Task<Result> HandleAsync(EventOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_seen.Add(entry.Envelope.EventId))
                SideEffectCount++;
            if (_failFirst)
            {
                _failFirst = false;
                return Task.FromResult(Result.Failure(new Error(
                    "test.outbox.delivery-failed", ErrorCategory.External,
                    "Simulated delivery failure after the side effect.")));
            }
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class CancelingHandler : IEventOutboxHandler
    {
        public Task<Result> HandleAsync(EventOutboxEntry entry, CancellationToken cancellationToken = default) =>
            throw new OperationCanceledException("Simulated worker termination after claiming the outbox row.");
    }
}
