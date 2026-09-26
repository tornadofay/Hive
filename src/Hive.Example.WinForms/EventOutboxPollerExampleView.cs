using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class EventOutboxPollerExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public EventOutboxPollerExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        _output = output;
        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        { Dock = DockStyle.Fill, RunButtonText = "Run outbox poller example" };
        _surface.SetInformation(
            "Creates an outbox event, simulates a failed first delivery, then retries after the lease expires.",
            "The handler records the EventId before failing. The retry sees the same EventId, avoids duplicating the side effect, and the poller removes the row only after successful delivery.",
            "Scope",
            "Phase 1.8 outbox delivery only; no MAF execution, provider call, broker, or background host loop");
        _surface.CodeSnippet = """
            var poller = new EventOutboxPoller(store);
            var result = await poller.ProcessNextAsync(handler, cancellationToken);
            """;
        _surface.ConfigureRun(RunExampleAsync, _output, FindForm());
        Controls.Add(_surface);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = HiveDatabaseOptions.LocalDevelopment("Hive_Example_EventOutboxPoller");
        var migration = await new HiveDatabaseMigrator(options).MigrateAsync(cancellationToken);
        EnsureSuccess(migration, "Hive database migration");

        var store = new SqlEventPersistenceStore(options);
        var serializer = new JsonEventSerializer();
        var eventEnvelope = serializer.CreateEnvelope(
            EventId.New(),
            DateTimeOffset.UtcNow,
            new EventType("example.outbox.poller"),
            new EventPayloadVersion(1),
            CorrelationId.New(), null,
            new { message = "Deliver me once." });
        var stream = new ResourceReference(ResourceKind.WorkItem, Guid.NewGuid());

        var append = await store.AppendAsync(new EventAppendRequest(stream, null, eventEnvelope), cancellationToken);
        EnsureSuccess(append, "Event append");

        var handler = new ExampleHandler();
        var poller = new EventOutboxPoller(store, TimeSpan.FromMilliseconds(75));
        var first = await poller.ProcessNextAsync(handler, cancellationToken);
        if (!first.IsFailure)
            throw new InvalidOperationException("The simulated first delivery unexpectedly succeeded.");

        await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        var second = await poller.ProcessNextAsync(handler, cancellationToken);
        EnsureSuccess(second, "Outbox retry");

        var remaining = await store.GetOutboxAsync(eventEnvelope.EventId, cancellationToken);
        EnsureSuccess(remaining, "Outbox lookup after delivery");

        _output.Write(
            "Transactional Outbox Poller",
            $"""
            Database: {options.DatabaseName}
            Event: {eventEnvelope.EventId}
            First delivery: simulated failure; lease retained
            Retry delivery: success; event identity preserved
            Idempotent side effects: {handler.SideEffectCount}
            Outbox after processing: {(remaining.Value is null ? "none" : "still present")}
            Migration: {migration.Value!.Status}; schema={migration.Value.CurrentSchemaVersion}
            """);
    }

    private sealed class ExampleHandler : IEventOutboxHandler
    {
        private readonly HashSet<EventId> _seenEvents = [];
        private bool _failFirstDelivery = true;
        public int SideEffectCount { get; private set; }

        public Task<Result> HandleAsync(EventOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_seenEvents.Add(entry.Envelope.EventId))
                SideEffectCount++;
            if (_failFirstDelivery)
            {
                _failFirstDelivery = false;
                return Task.FromResult(Result.Failure(new Error(
                    "example.outbox.simulated-failure", ErrorCategory.External,
                    "Simulated delivery failure after recording the EventId.")));
            }
            return Task.FromResult(Result.Success());
        }
    }

    private static void EnsureSuccess<T>(Result<T> result, string operation)
    {
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
    }
}
