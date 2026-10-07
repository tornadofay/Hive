using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class DurableAgentWorkStateExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public DurableAgentWorkStateExampleView(
        IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run durable Agent work example"
        };

        _surface.SetInformation(
            "Persists Objective, WorkItem binding, memory, Question state, and explicit delegation to SQL, then recreates a RuntimeInstance with the same RuntimeId.",
            "Runtime incarnation is ephemeral. Durable work state remains attached to the explicit RuntimeId. Delegation is persisted only as an explicit request; this example does not schedule or execute delegated work.",
            "Scope",
            "Phase 1.18 durable Base-Agent work state only");

        _surface.CodeSnippet = """
            var stores = HiveAgentWorkPersistence.CreateSql(options);
            var runtime = agent.CreateRuntimeInstance(
                workStores: stores,
                runtimeId: persistedRuntimeId);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var options = HiveDatabaseOptions.LocalDevelopment(
            $"Hive_Example_Phase118_{Guid.NewGuid():N}");

        var migration = await new HiveDatabaseMigrator(options)
            .MigrateAsync(cancellationToken);

        EnsureSuccess(
            migration,
            "Hive database migration");

        var now = new DateTimeOffset(
            2026,
            10,
            7,
            7,
            30,
            0,
            TimeSpan.Zero);

        var clock = new ExampleClock(now);
        var deployment = DeploymentId.New();
        var tenant = TenantId.New();
        var principal = PrincipalId.New();

        var agentResult = new AgentFactory(
                new AllowBaseAgentCreationAuthorizer(),
                clock)
            .Create<Agent>(
                new AgentDefinition(
                    "phase118-example-agent",
                    "Phase 1.18 Durable Work Agent"),
                new AgentCreationContext(
                    new ResourceAccessContext(
                        deployment,
                        tenant,
                        principal)));

        EnsureSuccess(agentResult, "Agent creation");

        var agent = agentResult.Value!;
        var stores = HiveAgentWorkPersistence.CreateSql(
            options,
            clock);
        var runtimeId = RuntimeId.New();

        var runtime = agent.CreateRuntimeInstance(
            now,
            clock: clock,
            workStores: stores,
            runtimeId: runtimeId);

        var context = new ResourceAccessContext(
            deployment,
            tenant,
            principal,
            AgentId: agent.Id,
            RuntimeId: runtimeId);

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Runtime(runtimeId),
            new ResourceProvenance(
                principal,
                now,
                CorrelationId.New()),
            now);

        var binding = runtime.Work.BindWorkItem(
            context,
            workItem,
            now,
            CorrelationId.New());

        EnsureSuccess(binding, "WorkItem binding");

        var objective = runtime.Work.Objectives.Create(
            context,
            agent.Id,
            runtimeId,
            "Prepare invoice",
            new ObjectiveUpdate(
                "Customer and amount are validated.",
                10,
                now.AddHours(1),
                []),
            now,
            binding.Value);

        EnsureSuccess(objective, "Objective creation");

        var memory = runtime.Work.Memory.Store(
            context,
            agent.Id,
            runtimeId,
            "customer.name",
            "Alice",
            now,
            evidenceKind: MemoryEvidenceKind.Actual);

        EnsureSuccess(memory, "Memory store");

        var question = runtime.Work.Questions.Ask(
            context,
            agent.Id,
            runtimeId,
            "Is the invoice ready to write?",
            TimeSpan.FromMinutes(5),
            now);

        EnsureSuccess(question, "Question creation");

        var delegation = DelegationRequest.Create(
            context,
            agent.Id,
            runtimeId,
            agent.Id,
            RuntimeId.New(),
            "Validate invoice total.",
            now);

        EnsureSuccess(delegation, "Delegation creation");

        var submittedDelegation = runtime.Work.Delegation.Submit(
            context,
            delegation.Value!);

        EnsureSuccess(
            submittedDelegation,
            "Delegation submission");

        var stopped = runtime.Stop(
            now.AddMinutes(1));

        EnsureSuccess(stopped, "Runtime stop");

        var recovered = agent.CreateRuntimeInstance(
            now.AddMinutes(2),
            clock: clock,
            workStores: stores,
            runtimeId: runtimeId);

        var recoveredObjective =
            recovered.Work.Objectives.Get(
                context,
                agent.Id,
                runtimeId,
                objective.Value!.Id);

        EnsureSuccess(
            recoveredObjective,
            "Objective recovery");

        var recoveredMemory =
            recovered.Work.Memory.Get(
                context,
                agent.Id,
                runtimeId,
                memory.Value!.Id);

        EnsureSuccess(
            recoveredMemory,
            "Memory recovery");

        var answered =
            recovered.Work.Questions.Answer(
                context,
                question.Value!.Id,
                agent.Id,
                runtimeId,
                "Yes",
                now.AddMinutes(3));

        EnsureSuccess(
            answered,
            "Question answer after restart");

        var waited =
            await recovered.Work.Questions.WaitAsync(
                context,
                agent.Id,
                runtimeId,
                question.Value.Id,
                cancellationToken);

        EnsureSuccess(
            waited,
            "Question terminal recovery");

        var recoveredDelegation =
            recovered.Work.Delegation.Read(
                context,
                agent.Id,
                runtimeId,
                submittedDelegation.Value!.Id);

        EnsureSuccess(
            recoveredDelegation,
            "Delegation recovery");

        _output.Write(
            "Durable Base-Agent Work State",
            $"""
            Database: {options.DatabaseName}
            Agent: {agent.Id}; generation={agent.Generation}
            RuntimeId reused: {runtimeId}
            Objective: {recoveredObjective.Value!.Id}; version={recoveredObjective.Value.Resource.Version}
            WorkItem binding: {recoveredObjective.Value.WorkItemBinding!.WorkItemId}; observedVersion={recoveredObjective.Value.WorkItemBinding.WorkItemVersion}
            Memory: {recoveredMemory.Value!.Id}; evidence={recoveredMemory.Value.EvidenceKind}
            Question: {waited.Value!.Status}; answer={waited.Value.Answer}
            Delegation: {recoveredDelegation.Value!.Id}; request remains explicit and participant-scoped
            """);
    }

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

    private sealed class ExampleClock : IClock
    {
        public ExampleClock(DateTimeOffset utcNow) =>
            UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; private set; }
    }
}
