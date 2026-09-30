using Hive.Core;
using Hive.Management;
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
            "Creates a WorkItem through Hive.Management so its durable creation produces the transactional outbox entry, then simulates a failed first delivery and retries it.",
            "The first delivery runs longer than its 75ms lease, so renewal keeps the claim alive before the simulated failure. The retry sees the same EventId, avoids duplicating the side effect, and the poller acknowledges the row only after successful delivery.",
            "Scope",
            "Phase 1.8 outbox delivery only; no MAF execution, provider call, broker, or background host loop");
        _surface.CodeSnippet = """
            var persistence = HiveEventPersistence.CreateSql(options);
            var poller = persistence.CreateOutboxPoller();
            var result = await poller.ProcessNextAsync(handler, cancellationToken);
            """;
        _surface.ConfigureRun(RunExampleAsync, _output, FindForm());
        Controls.Add(_surface);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = HiveDatabaseOptions.LocalDevelopment(
            $"Hive_Example_EventOutboxPoller_{Guid.NewGuid():N}");
        var migration = await new HiveDatabaseMigrator(options).MigrateAsync(cancellationToken);
        EnsureSuccess(migration, "Hive database migration");

        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        using var management = new HiveManagementFacade(
            new SqlProviderResourceStore(options),
            new SqlAgentDefinitionResourceStore(options),
            new SqlWorkItemResourceStore(options));

        var workItem = await management.CreateImageWorkItemAsync(
            new WorkItemImageSubmission(
                "outbox-example.png",
                "image/png",
                new byte[] { 1, 2, 3, 4 }),
            context,
            cancellationToken);

        EnsureSuccess(workItem, "WorkItem creation");

        var activity = await management.GetWorkItemActivityAsync(
            workItem.Value!.Id,
            context,
            cancellationToken);

        EnsureSuccess(activity, "WorkItem activity read");

        var createdActivity = activity.Value!
            .Single(static item => item.EventType == "work-item.created");

        var eventId = createdActivity.EventId;

        var persistence = HiveEventPersistence.CreateSql(options);
        var poller = persistence.CreateOutboxPoller(
            TimeSpan.FromMilliseconds(75));
        var handler = new ExampleHandler(TimeSpan.FromMilliseconds(120));

        var first = await poller.ProcessNextAsync(
            handler,
            cancellationToken);

        if (!first.IsFailure)
            throw new InvalidOperationException(
                "The simulated first delivery unexpectedly succeeded.");

        await Task.Delay(
            TimeSpan.FromMilliseconds(100),
            cancellationToken);

        var second = await poller.ProcessNextAsync(
            handler,
            cancellationToken);

        EnsureSuccess(second, "Outbox retry");

        if (second.Value?.Envelope.EventId != eventId)
        {
            throw new InvalidOperationException(
                "The retry claimed a different outbox event; the example database was not isolated.");
        }

        var remaining = await poller.ProcessNextAsync(
            handler,
            cancellationToken);

        EnsureSuccess(remaining, "Outbox empty check");

        if (remaining.Value is not null)
        {
            throw new InvalidOperationException(
                "The retried outbox event remained after successful acknowledgement.");
        }

        _output.Write(
            "Transactional Outbox Poller",
            $"""
            Database: {options.DatabaseName}
            WorkItem: {workItem.Value.Id}
            Event: {eventId}
            First delivery: delayed beyond lease; renewal kept claim; simulated failure
            Retry delivery: success; event identity preserved
            Idempotent side effects: {handler.SideEffectCount}
            Outbox after processing: {(remaining.Value is null ? "none" : "still present")}
            Migration: {migration.Value!.Status}; schema={migration.Value.CurrentSchemaVersion}
            """);
    }

    private sealed class ExampleHandler : IEventOutboxHandler
    {
        private readonly HashSet<EventId> _seenEvents = [];
        private readonly TimeSpan _firstDeliveryDelay;
        private bool _failFirstDelivery = true;
        public int SideEffectCount { get; private set; }

        public ExampleHandler(TimeSpan firstDeliveryDelay) =>
            _firstDeliveryDelay = firstDeliveryDelay;

        public async Task<Result> HandleAsync(
            EventOutboxEntry entry,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_failFirstDelivery && _firstDeliveryDelay > TimeSpan.Zero)
                await Task.Delay(
                    _firstDeliveryDelay,
                    cancellationToken);
            if (_seenEvents.Add(entry.Envelope.EventId))
                SideEffectCount++;
            if (_failFirstDelivery)
            {
                _failFirstDelivery = false;
                return Result.Failure(new Error(
                    "example.outbox.simulated-failure", ErrorCategory.External,
                    "Simulated delivery failure after recording the EventId."));
            }

            return Result.Success();
        }
    }

    private static void EnsureSuccess<T>(Result<T> result, string operation)
    {
        if (result.IsFailure)
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
    }
}
