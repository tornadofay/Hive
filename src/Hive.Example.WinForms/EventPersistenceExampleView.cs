using System.Text.Json;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class EventPersistenceExampleView : UserControl
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public EventPersistenceExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run event persistence example"
        };

        _surface.SetInformation(
            "Creates a local SQL Server event stream, appends immutable events, stores a versioned snapshot and transactional outbox entry, then reloads and folds the event history.",
            "The event, snapshot replacement, and outbox row are committed together. A second append advances the same stream from version 1 to version 2.",
            "Scope",
            "Phase 1.7 durable event persistence only; no outbox polling, MAF execution, provider call, or cognitive behavior");

        _surface.CodeSnippet = """
            var store = new SqlEventPersistenceStore(
                HiveDatabaseOptions.LocalDevelopment("Hive_Example_EventPersistence"));

            var append = await store.AppendAsync(
                new EventAppendRequest(
                    stream,
                    expectedVersion: null,
                    envelope,
                    snapshot));
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = HiveDatabaseOptions.LocalDevelopment(
            "Hive_Example_EventPersistence");

        var migration = await new HiveDatabaseMigrator(options)
            .MigrateAsync(cancellationToken);

        EnsureSuccess(migration, "Hive database migration");

        var store = new SqlEventPersistenceStore(options);
        var stream = new ResourceReference(
            ResourceKind.WorkItem,
            Guid.NewGuid());

        var serializer = new JsonEventSerializer();
        var eventType = new EventType("example.work-item.counter-incremented");

        var firstEvent = serializer.CreateEnvelope(
            EventId.New(),
            new DateTimeOffset(
                2026,
                9,
                22,
                7,
                0,
                0,
                TimeSpan.Zero),
            eventType,
            new EventPayloadVersion(1),
            CorrelationId.New(),
            null,
            new CounterIncrement(2));

        var firstSnapshot = CreateSnapshot(
            stream,
            ResourceVersion.Initial,
            new CounterState(2));

        var firstAppend = await store.AppendAsync(
            new EventAppendRequest(
                stream,
                expectedVersion: null,
                firstEvent,
                firstSnapshot),
            cancellationToken);

        EnsureSuccess(firstAppend, "First event append");

        var secondEvent = serializer.CreateEnvelope(
            EventId.New(),
            firstEvent.OccurredAtUtc.AddMinutes(1),
            eventType,
            new EventPayloadVersion(1),
            firstEvent.CorrelationId,
            new CausationId(firstEvent.EventId.Value),
            new CounterIncrement(3));

        var secondAppend = await store.AppendAsync(
            new EventAppendRequest(
                stream,
                firstAppend.Value!.Event.StreamVersion,
                secondEvent,
                CreateSnapshot(
                    stream,
                    new ResourceVersion(2),
                    new CounterState(5))),
            cancellationToken);

        EnsureSuccess(secondAppend, "Second event append");

        var events = await store.ReadEventsAsync(
            stream,
            cancellationToken: cancellationToken);

        EnsureSuccess(events, "Event history read");

        var snapshot = await store.GetSnapshotAsync(
            stream,
            cancellationToken);

        EnsureSuccess(snapshot, "Snapshot read");

        var outbox = await store.GetOutboxAsync(
            secondEvent.EventId,
            cancellationToken);

        EnsureSuccess(outbox, "Outbox read");

        var reducers = new EventStateReducerRegistry<CounterState>();
        reducers.Register(new CounterReducer(eventType));

        var folder = new EventSnapshotFolder<CounterState>(
            reducers,
            serializer);

        var folded = folder.Fold(
            new CounterState(0),
            events.Value!.Select(item => item.Envelope).ToArray());

        EnsureSuccess(folded, "Deterministic event fold");

        _output.Write(
            "Event Log / Snapshot / Outbox",
            $"""
            Database: {options.DatabaseName}
            Stream: {stream.Kind}/{stream.Identity}
            Event 1: {firstAppend.Value!.Event.Envelope.EventId}; version={firstAppend.Value.Event.StreamVersion}
            Event 2: {secondAppend.Value!.Event.Envelope.EventId}; version={secondAppend.Value.Event.StreamVersion}
            Stored event count: {events.Value.Count}
            Snapshot version: {snapshot.Value!.Version}; count={snapshot.Value.State.GetProperty("count").GetInt32()}
            Outbox event: {outbox.Value!.Envelope.EventId}; type={outbox.Value.Envelope.EventType}
            Deterministic fold count: {folded.Value!.Count}
            Migration: {migration.Value!.Status}; schema={migration.Value.CurrentSchemaVersion}
            """);
    }

    private static EventSnapshot CreateSnapshot(
        ResourceReference stream,
        ResourceVersion version,
        CounterState state) =>
        new(
            stream,
            version,
            new EventPayloadVersion(1),
            JsonSerializer.SerializeToElement(
                state,
                SnapshotJsonOptions));

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
    }

    private sealed record CounterIncrement(int Amount);

    private sealed record CounterState(int Count);

    private sealed class CounterReducer : IEventStateReducer<CounterState>
    {
        public CounterReducer(EventType eventType)
        {
            EventType = eventType;
        }

        public EventType EventType { get; }

        public EventPayloadVersion CurrentPayloadSchemaVersion =>
            new(1);

        public CounterState Apply(
            CounterState state,
            EventEnvelope envelope,
            JsonElement payload) =>
            state with
            {
                Count = checked(
                    state.Count +
                    payload.GetProperty("amount").GetInt32())
            };
    }
}
