using Hive.Agents;
using Hive.Core;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class Phase118DurableBaseAgentWorkStateTests
{
    [Fact]
    public void Objective_SurvivesRuntimeRecreation_WithWorkItemBinding()
    {
        using var fixture = CreateFixture("Objective");

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            fixture.Principal,
            ResourceScope.Runtime(fixture.RuntimeId),
            new ResourceProvenance(
                fixture.Principal,
                fixture.Now,
                CorrelationId.New()),
            fixture.Now);

        var binding = fixture.Runtime.Work.BindWorkItem(
            fixture.Context,
            workItem,
            fixture.Now,
            CorrelationId.New());

        Assert.True(binding.IsSuccess, binding.Error?.Message);

        var objective = fixture.Runtime.Work.Objectives.Create(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "Prepare invoice",
            new ObjectiveUpdate(
                "Customer and amount are validated.",
                10,
                null,
                []),
            fixture.Now,
            binding.Value);

        Assert.True(objective.IsSuccess, objective.Error?.Message);

        var updated = fixture.Runtime.Work.Objectives.Update(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            objective.Value!.Id,
            new ObjectiveUpdate(
                "Customer, amount, and tax are validated.",
                20,
                null,
                []),
            fixture.Now.AddMinutes(1));

        Assert.True(updated.IsSuccess, updated.Error?.Message);

        fixture.Runtime = fixture.Agent.CreateRuntimeInstance(
            fixture.Now.AddMinutes(2),
            clock: fixture.Clock,
            workStores: fixture.Stores,
            runtimeId: fixture.RuntimeId);

        var restored = fixture.Runtime.Work.Objectives.Get(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            objective.Value!.Id);

        Assert.True(restored.IsSuccess, restored.Error?.Message);
        Assert.Equal(ResourceVersion.Initial.Next(), restored.Value!.Resource.Version);
        Assert.Equal(workItem.Id, restored.Value.WorkItemBinding!.WorkItemId);
        Assert.Equal(
            workItem.Resource.Version,
            restored.Value.WorkItemBinding.WorkItemVersion);
    }

    [Fact]
    public void Memory_SurvivesRuntimeRecreation_WithDeterministicOrderingAndEvidence()
    {
        using var fixture = CreateFixture("Memory");

        var later = fixture.Runtime.Work.Memory.Store(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "customer",
            "simulated",
            fixture.Now.AddMinutes(2),
            evidenceKind: MemoryEvidenceKind.Simulated);

        var earlier = fixture.Runtime.Work.Memory.Store(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "customer",
            "actual",
            fixture.Now.AddMinutes(1),
            evidenceKind: MemoryEvidenceKind.Actual);

        Assert.True(later.IsSuccess, later.Error?.Message);
        Assert.True(earlier.IsSuccess, earlier.Error?.Message);

        fixture.Runtime = fixture.Agent.CreateRuntimeInstance(
            fixture.Now.AddMinutes(3),
            clock: fixture.Clock,
            workStores: fixture.Stores,
            runtimeId: fixture.RuntimeId);

        var restored = fixture.Runtime.Work.Memory.Retrieve(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "customer");

        Assert.True(restored.IsSuccess, restored.Error?.Message);
        Assert.Equal(2, restored.Value!.Count);
        Assert.Equal("actual", restored.Value[0].Content);
        Assert.Equal(MemoryEvidenceKind.Actual, restored.Value[0].EvidenceKind);
        Assert.Equal("simulated", restored.Value[1].Content);
        Assert.Equal(MemoryEvidenceKind.Simulated, restored.Value[1].EvidenceKind);
    }

    [Fact]
    public async Task Question_AnswersAfterRuntimeRecreation_AndWaitReturnsTerminalState()
    {
        using var fixture = CreateFixture("Question");

        var question = fixture.Runtime.Work.Questions.Ask(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "Approve the invoice?",
            TimeSpan.FromHours(1),
            fixture.Now);

        Assert.True(question.IsSuccess, question.Error?.Message);

        fixture.Runtime = fixture.Agent.CreateRuntimeInstance(
            fixture.Now.AddMinutes(1),
            clock: fixture.Clock,
            workStores: fixture.Stores,
            runtimeId: fixture.RuntimeId);

        var answered = fixture.Runtime.Work.Questions.Answer(
            fixture.Context,
            question.Value!.Id,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "Yes",
            fixture.Now.AddMinutes(2));

        Assert.True(answered.IsSuccess, answered.Error?.Message);

        var waited = await fixture.Runtime.Work.Questions.WaitAsync(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            question.Value!.Id);

        Assert.True(waited.IsSuccess, waited.Error?.Message);
        Assert.Equal(QuestionStatus.Answered, waited.Value!.Status);
        Assert.Equal("Yes", waited.Value.Answer);
    }

    [Fact]
    public async Task QuestionExpiry_IsDeterministicAfterRuntimeRecreation()
    {
        using var fixture = CreateFixture("Expiry");

        var question = fixture.Runtime.Work.Questions.Ask(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "Need approval?",
            TimeSpan.FromMinutes(5),
            fixture.Now);

        Assert.True(question.IsSuccess, question.Error?.Message);

        fixture.Clock.Advance(TimeSpan.FromMinutes(5));

        fixture.Runtime = fixture.Agent.CreateRuntimeInstance(
            fixture.Clock.UtcNow,
            clock: fixture.Clock,
            workStores: fixture.Stores,
            runtimeId: fixture.RuntimeId);

        Assert.Equal(1, fixture.Runtime.Work.Questions.ExpireDue());

        var restored = await fixture.Runtime.Work.Questions.WaitAsync(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            question.Value!.Id);

        Assert.True(restored.IsSuccess, restored.Error?.Message);
        Assert.Equal(QuestionStatus.TimedOut, restored.Value!.Status);
    }

    [Fact]
    public async Task QuestionWaitCancellation_DoesNotChangeDurableState()
    {
        using var fixture = CreateFixture("QuestionCancellation");

        var question = fixture.Runtime.Work.Questions.Ask(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "Will it be approved?",
            TimeSpan.FromHours(1),
            fixture.Now);

        Assert.True(question.IsSuccess, question.Error?.Message);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var cancelledWait = await fixture.Runtime.Work.Questions.WaitAsync(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            question.Value!.Id,
            cancellation.Token);

        Assert.True(cancelledWait.IsFailure);
        Assert.Equal(
            ErrorCategory.Cancelled,
            cancelledWait.Error!.Category);

        var unchanged = fixture.Runtime.Work.Questions.WaitAsync(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            question.Value.Id);

        var cancelled = fixture.Runtime.Work.Questions.Cancel(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            question.Value.Id,
            fixture.Now.AddMinutes(1));

        Assert.True(cancelled.IsSuccess, cancelled.Error?.Message);

        var terminal = await unchanged;

        Assert.True(terminal.IsSuccess, terminal.Error?.Message);
        Assert.Equal(QuestionStatus.Cancelled, terminal.Value!.Status);
    }

    [Fact]
    public void Delegation_SurvivesRestart_AndRemainsParticipantScoped()
    {
        using var fixture = CreateFixture("Delegation");

        var delegateRuntimeId = RuntimeId.New();

        var request = DelegationRequest.Create(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            fixture.Agent.Id,
            delegateRuntimeId,
            "Validate invoice total.",
            fixture.Now);

        Assert.True(request.IsSuccess, request.Error?.Message);

        var submitted = fixture.Runtime.Work.Delegation.Submit(
            fixture.Context,
            request.Value!);

        Assert.True(submitted.IsSuccess, submitted.Error?.Message);

        fixture.Runtime = fixture.Agent.CreateRuntimeInstance(
            fixture.Now.AddMinutes(1),
            clock: fixture.Clock,
            workStores: fixture.Stores,
            runtimeId: fixture.RuntimeId);

        var requesterRead = fixture.Runtime.Work.Delegation.Read(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            request.Value!.Id);

        Assert.True(requesterRead.IsSuccess, requesterRead.Error?.Message);

        var outsiderPrincipal = PrincipalId.New();

        var outsiderAgent = CreateAgent(
            fixture.Deployment,
            fixture.Tenant,
            outsiderPrincipal,
            fixture.Clock);

        var outsiderRuntime = outsiderAgent.CreateRuntimeInstance(
            fixture.Now,
            clock: fixture.Clock,
            workStores: fixture.Stores);

        var outsiderContext = new ResourceAccessContext(
            fixture.Deployment,
            fixture.Tenant,
            outsiderPrincipal,
            AgentId: outsiderAgent.Id,
            RuntimeId: outsiderRuntime.Id);

        var denied = outsiderRuntime.Work.Delegation.Read(
            outsiderContext,
            outsiderAgent.Id,
            outsiderRuntime.Id,
            request.Value!.Id);

        Assert.True(denied.IsFailure);
        Assert.Equal(
            "hive.agent.delegation.not-authorized",
            denied.Error!.Code);
    }

    [Fact]
    public void SharedDurableStores_IsolateRuntimeState()
    {
        using var fixture = CreateFixture("Isolation");

        var otherRuntimeId = RuntimeId.New();
        var otherRuntime = fixture.Agent.CreateRuntimeInstance(
            fixture.Now,
            clock: fixture.Clock,
            workStores: fixture.Stores,
            runtimeId: otherRuntimeId);

        var otherContext = fixture.WithRuntime(otherRuntimeId);

        var stored = fixture.Runtime.Work.Memory.Store(
            fixture.Context,
            fixture.Agent.Id,
            fixture.RuntimeId,
            "customer",
            "first runtime only",
            fixture.Now);

        Assert.True(stored.IsSuccess, stored.Error?.Message);

        var otherRead = otherRuntime.Work.Memory.Retrieve(
            otherContext,
            fixture.Agent.Id,
            otherRuntimeId,
            "customer");

        Assert.True(otherRead.IsSuccess, otherRead.Error?.Message);
        Assert.Empty(otherRead.Value!);

        var foreignGet = otherRuntime.Work.Memory.Get(
            otherContext,
            fixture.Agent.Id,
            otherRuntimeId,
            stored.Value!.Id);

        Assert.True(foreignGet.IsFailure);
        Assert.Equal(
            "hive.agent.memory.runtime-mismatch",
            foreignGet.Error!.Code);
    }

    private static Fixture CreateFixture(string name)
    {
        var database = new PersistenceTestDatabase(
            $"Hive_Test_Phase118_{name}_{Guid.NewGuid():N}");

        try
        {
            database.Reset();

            var migration = new HiveDatabaseMigrator(database.Options)
                .MigrateAsync()
                .GetAwaiter()
                .GetResult();

            Assert.True(migration.IsSuccess, migration.Error?.Message);

            var now = new DateTimeOffset(
                2026,
                10,
                7,
                7,
                0,
                0,
                TimeSpan.Zero);

            var clock = new FixedClock(now);
            var deployment = DeploymentId.New();
            var tenant = TenantId.New();
            var principal = PrincipalId.New();

            var agent = CreateAgent(
                deployment,
                tenant,
                principal,
                clock);

            var stores = HiveAgentWorkPersistence.CreateSql(
                database.Options,
                clock);

            var runtimeId = RuntimeId.New();

            var runtime = agent.CreateRuntimeInstance(
                now,
                clock: clock,
                workStores: stores,
                runtimeId: runtimeId);

            return new Fixture(
                database,
                now,
                clock,
                agent,
                runtime,
                runtimeId,
                stores,
                deployment,
                tenant,
                principal);
        }
        catch (Exception creationFailure)
        {
            try
            {
                database.Dispose();
            }
            catch (Exception cleanupFailure)
            {
                throw new AggregateException(
                    "Phase 1.18 test fixture creation failed and its SQL database could not be cleaned up.",
                    creationFailure,
                    cleanupFailure);
            }

            throw;
        }
    }

    private static Agent CreateAgent(
        DeploymentId deployment,
        TenantId tenant,
        PrincipalId principal,
        IClock clock) =>
        new AgentFactory(
            new AllowBaseAgentCreationAuthorizer(),
            clock)
            .Create<Agent>(
                new AgentDefinition(
                    "phase118-agent",
                    "Phase 1.18 Agent"),
                new AgentCreationContext(
                    new ResourceAccessContext(
                        deployment,
                        tenant,
                        principal)))
            .Value!;

    private sealed class Fixture : IDisposable
    {
        public Fixture(
            PersistenceTestDatabase database,
            DateTimeOffset now,
            FixedClock clock,
            Agent agent,
            RuntimeInstance runtime,
            RuntimeId runtimeId,
            RuntimeWorkProtocolStores stores,
            DeploymentId deployment,
            TenantId tenant,
            PrincipalId principal)
        {
            Database = database;
            Now = now;
            Clock = clock;
            Agent = agent;
            Runtime = runtime;
            RuntimeId = runtimeId;
            Stores = stores;
            Deployment = deployment;
            Tenant = tenant;
            Principal = principal;
            Context = new ResourceAccessContext(
                deployment,
                tenant,
                principal,
                AgentId: agent.Id,
                RuntimeId: runtimeId);
        }

        public PersistenceTestDatabase Database { get; }

        public void Dispose() => Database.Dispose();

        public DateTimeOffset Now { get; }

        public FixedClock Clock { get; }

        public Agent Agent { get; }

        public RuntimeInstance Runtime { get; set; }

        public RuntimeId RuntimeId { get; }

        public RuntimeWorkProtocolStores Stores { get; }

        public DeploymentId Deployment { get; }

        public TenantId Tenant { get; }

        public PrincipalId Principal { get; }

        public ResourceAccessContext Context { get; }

        public ResourceAccessContext WithRuntime(
            RuntimeId runtimeId) =>
            Context with { RuntimeId = runtimeId };
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) =>
            UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan amount) =>
            UtcNow = UtcNow.Add(amount);
    }
}
