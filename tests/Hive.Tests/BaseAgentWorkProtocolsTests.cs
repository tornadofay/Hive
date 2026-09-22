using Hive.Agents;
using Hive.Core;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class BaseAgentWorkProtocolsTests
{
    [Fact]
    public void ObjectiveLifecycle_UpdateBindAndComplete_PreservesRuntimeOwnershipAndProvenance()
    {
        var fixture = CreateRuntime();
        var firstObjectiveState = new ObjectiveUpdate(
            "Customer and amount are known.",
            10,
            fixture.Clock.UtcNow.AddHours(1),
            []);

        var created = fixture.Runtime.Work.Objectives.Create(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            "Prepare invoice",
            firstObjectiveState,
            fixture.Clock.UtcNow);

        Assert.True(created.IsSuccess, created.Error?.Message);

        var objective = created.Value!;
        Assert.Equal(ObjectiveStatus.Active, objective.Status);
        Assert.Equal(ResourceScopeKind.Runtime, objective.Resource.Scope.Kind);
        Assert.Equal(fixture.Runtime.Id.Value, objective.Resource.Scope.Identity);
        Assert.Equal(ResourceVersion.Initial, objective.Resource.Version);

        var update = objective.Resource.Version;
        var updated = fixture.Runtime.Work.Objectives.Update(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            objective.Id,
            new ObjectiveUpdate(
                "Customer and amount are validated.",
                20,
                fixture.Clock.UtcNow.AddHours(2),
                [ObjectiveId.New()]),
            fixture.Clock.UtcNow.AddMinutes(1));

        Assert.True(updated.IsSuccess, updated.Error?.Message);
        Assert.Equal(update.Next(), updated.Value!.Resource.Version);

        var workItem = CreateWorkItem(fixture);
        var binding = fixture.Runtime.Work.BindWorkItem(
            fixture.Context,
            workItem,
            fixture.Clock.UtcNow.AddMinutes(2),
            CorrelationId.New());

        Assert.True(binding.IsSuccess, binding.Error?.Message);

        var bound = fixture.Runtime.Work.Objectives.BindWorkItem(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            objective.Id,
            binding.Value!,
            fixture.Clock.UtcNow.AddMinutes(2));

        Assert.True(bound.IsSuccess, bound.Error?.Message);
        Assert.Equal(workItem.Id, bound.Value!.WorkItemBinding!.WorkItemId);
        Assert.Equal(
            workItem.Resource.Version,
            bound.Value.WorkItemBinding.WorkItemVersion);
        Assert.Equal(
            workItem.Id.Value,
            bound.Value.WorkItemBinding.Provenance.Source!.Value.Identity);
        Assert.Equal(
            workItem.Resource.Provenance.CreatedBy,
            bound.Value.WorkItemBinding.Provenance.CreatedBy);

        var completed = fixture.Runtime.Work.Objectives.Complete(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            objective.Id,
            fixture.Clock.UtcNow.AddMinutes(3));

        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal(ObjectiveStatus.Completed, completed.Value!.Status);
        Assert.True(
            completed.Value.Resource.Version.Value >
            bound.Value.Resource.Version.Value);
    }

    [Fact]
    public void ObjectiveLifecycle_RejectsForeignRuntimeAndTerminalMutation()
    {
        var first = CreateRuntime();
        var second = CreateRuntime(first.DeploymentId, first.TenantId);

        var created = first.Runtime.Work.Objectives.Create(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            "Objective",
            new ObjectiveUpdate("Done.", 1, null, []),
            first.Clock.UtcNow);

        Assert.True(created.IsSuccess, created.Error?.Message);

        var foreign = second.Runtime.Work.Objectives.Get(
            second.Context,
            second.Agent.Id,
            second.Runtime.Id,
            created.Value!.Id);

        Assert.True(foreign.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, foreign.Error!.Category);

        var completed = first.Runtime.Work.Objectives.Complete(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            created.Value.Id,
            first.Clock.UtcNow.AddMinutes(1));

        Assert.True(completed.IsSuccess, completed.Error?.Message);

        var update = first.Runtime.Work.Objectives.Update(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            created.Value.Id,
            new ObjectiveUpdate("No longer mutable.", 2, null, []),
            first.Clock.UtcNow.AddMinutes(2));

        Assert.True(update.IsFailure);
        Assert.Equal(
            "hive.agent.objective.invalid-transition",
            update.Error!.Code);
    }

    [Fact]
    public void WorkItemBinding_RequiresOwnerScopeAndMatchingRuntime()
    {
        var fixture = CreateRuntime();
        var otherPrincipal = PrincipalId.New();

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            otherPrincipal,
            ResourceScope.Runtime(fixture.Runtime.Id),
            new ResourceProvenance(
                otherPrincipal,
                fixture.Clock.UtcNow,
                CorrelationId.New()),
            fixture.Clock.UtcNow);

        var result = fixture.Runtime.Work.BindWorkItem(
            fixture.Context,
            workItem,
            fixture.Clock.UtcNow,
            CorrelationId.New());

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.agent.workitem.owner-mismatch",
            result.Error!.Code);

        var foreignRuntime = CreateRuntime(
            fixture.DeploymentId,
            fixture.TenantId,
            fixture.Context.PrincipalId!.Value);

        var ownedWorkItem = CreateWorkItem(fixture);
        var foreignContextResult = foreignRuntime.Runtime.Work.BindWorkItem(
            foreignRuntime.Context,
            ownedWorkItem,
            fixture.Clock.UtcNow,
            CorrelationId.New());

        Assert.True(foreignContextResult.IsFailure);
        Assert.Equal(
            "hive.agent.workitem.scope-mismatch",
            foreignContextResult.Error!.Code);
    }

    [Fact]
    public void MemoryStore_IsolatedPerRuntimeAndRecordsRuntimeScope()
    {
        var first = CreateRuntime();
        var second = CreateRuntime(
            first.DeploymentId,
            first.TenantId,
            first.Context.PrincipalId!.Value);

        var stored = first.Runtime.Work.Memory.Store(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            "customer",
            "Alice",
            first.Clock.UtcNow);

        Assert.True(stored.IsSuccess, stored.Error?.Message);
        Assert.Equal(first.Runtime.Id.Value, stored.Value!.Resource.Scope.Identity);

        var firstRead = first.Runtime.Work.Memory.Retrieve(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            "customer");

        Assert.True(firstRead.IsSuccess, firstRead.Error?.Message);
        Assert.Single(firstRead.Value!);

        var secondRead = second.Runtime.Work.Memory.Retrieve(
            second.Context,
            second.Agent.Id,
            second.Runtime.Id,
            "customer");

        Assert.True(secondRead.IsSuccess, secondRead.Error?.Message);
        Assert.Empty(secondRead.Value!);

        var secondGet = second.Runtime.Work.Memory.Get(
            second.Context,
            second.Agent.Id,
            second.Runtime.Id,
            stored.Value.Id);

        Assert.True(secondGet.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, secondGet.Error!.Category);
    }

    [Fact]
    public async Task QuestionTransport_AnswersWaitsAndRejectsTerminalTransition()
    {
        var fixture = CreateRuntime();
        var question = fixture.Runtime.Work.Questions.Ask(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            "What is the customer name?",
            TimeSpan.FromMinutes(5),
            fixture.Clock.UtcNow);

        Assert.True(question.IsSuccess, question.Error?.Message);

        var answered = fixture.Runtime.Work.Questions.Answer(
            fixture.Context,
            question.Value!.Id,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            "Alice",
            fixture.Clock.UtcNow.AddMinutes(1));

        Assert.True(answered.IsSuccess, answered.Error?.Message);

        var waited = await fixture.Runtime.Work.Questions.WaitAsync(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            question.Value.Id);

        Assert.True(waited.IsSuccess, waited.Error?.Message);
        Assert.Equal(QuestionStatus.Answered, waited.Value!.Status);
        Assert.Equal("Alice", waited.Value.Answer);

        var lateAnswer = fixture.Runtime.Work.Questions.Answer(
            fixture.Context,
            question.Value.Id,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            "Bob",
            fixture.Clock.UtcNow.AddMinutes(2));

        Assert.True(lateAnswer.IsFailure);
        Assert.Equal(
            "hive.agent.question.invalid-transition",
            lateAnswer.Error!.Code);
    }

    [Fact]
    public async Task QuestionTransport_TimeoutIsDeterministicAndCrossRuntimeWaitDoesNotLeak()
    {
        var first = CreateRuntime();
        var second = CreateRuntime(
            first.DeploymentId,
            first.TenantId,
            first.Context.PrincipalId!.Value);

        var question = first.Runtime.Work.Questions.Ask(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            "Need approval?",
            TimeSpan.FromMinutes(10),
            first.Clock.UtcNow);

        Assert.True(question.IsSuccess, question.Error?.Message);

        first.Clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(1, first.Runtime.Work.Questions.ExpireDue());

        var timedOut = await first.Runtime.Work.Questions.WaitAsync(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            question.Value!.Id);

        Assert.True(timedOut.IsSuccess, timedOut.Error?.Message);
        Assert.Equal(QuestionStatus.TimedOut, timedOut.Value!.Status);

        var foreignWait = await second.Runtime.Work.Questions.WaitAsync(
            second.Context,
            second.Agent.Id,
            second.Runtime.Id,
            question.Value.Id);

        Assert.True(foreignWait.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, foreignWait.Error!.Category);
    }

    [Fact]
    public void QuestionTransport_SharedTransportAllowsAuthorizedCrossRuntimeResponder()
    {
        var questions = new QuestionTransport();
        var first = CreateRuntime(questions: questions);
        var second = CreateRuntime(
            first.DeploymentId,
            first.TenantId,
            first.Context.PrincipalId!.Value,
            questions: questions);

        var question = first.Runtime.Work.Questions.Ask(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            "Can the delegate confirm the invoice?",
            TimeSpan.FromMinutes(5),
            first.Clock.UtcNow);

        Assert.True(question.IsSuccess, question.Error?.Message);

        var answered = second.Runtime.Work.Questions.Answer(
            second.Context,
            question.Value!.Id,
            second.Agent.Id,
            second.Runtime.Id,
            "Confirmed.",
            second.Clock.UtcNow.AddMinutes(1));

        Assert.True(answered.IsSuccess, answered.Error?.Message);
        Assert.Equal(QuestionStatus.Answered, answered.Value!.Status);
        Assert.Equal(second.Agent.Id, answered.Value.AnsweredByAgentId);
        Assert.Equal(second.Runtime.Id, answered.Value.AnsweredByRuntimeId);

        var invalidResponder = second.Runtime.Work.Questions.Answer(
            first.Context,
            question.Value.Id,
            second.Agent.Id,
            second.Runtime.Id,
            "Should be rejected.",
            second.Clock.UtcNow.AddMinutes(2));

        Assert.True(invalidResponder.IsFailure);
        Assert.Equal(
            "hive.agent.protocol.runtime-mismatch",
            invalidResponder.Error!.Code);
    }

    [Fact]
    public void UnderstandingGate_BlocksUntilMinimumInformationAndConfirmationExist()
    {
        var gate = new UnderstandingGate();
        var policy = new UnderstandingGatePolicy(
            ["customer.name", "invoice.total"],
            confirmationRequired: true);

        var blocked = gate.Evaluate(
            policy,
            ["customer.name"],
            confirmationReceived: false);

        Assert.Equal(UnderstandingGateStatus.Blocked, blocked.Status);
        Assert.Equal(["invoice.total"], blocked.MissingInformation);
        Assert.False(blocked.ConfirmationSatisfied);

        var stillBlocked = gate.Evaluate(
            policy,
            ["customer.name", "invoice.total"],
            confirmationReceived: false);

        Assert.Equal(UnderstandingGateStatus.Blocked, stillBlocked.Status);
        Assert.Empty(stillBlocked.MissingInformation);
        Assert.False(stillBlocked.ConfirmationSatisfied);

        var satisfied = gate.Evaluate(
            policy,
            ["invoice.total", "customer.name"],
            confirmationReceived: true);

        Assert.True(satisfied.IsSatisfied);
        Assert.Empty(satisfied.MissingInformation);
    }

    [Fact]
    public void DelegationChannel_PreservesProvenanceAndLimitsReadsToParticipants()
    {
        var delegation = new InMemoryDelegationChannel();
        var first = CreateRuntime(delegation: delegation);
        var second = CreateRuntime(
            first.DeploymentId,
            first.TenantId,
            first.Context.PrincipalId!.Value,
            delegation: delegation);
        var outsider = CreateRuntime(
            first.DeploymentId,
            first.TenantId,
            PrincipalId.New(),
            delegation: delegation);

        var workItem = CreateWorkItem(first);
        var request = DelegationRequest.Create(
            first.Context,
            first.Agent.Id,
            first.Runtime.Id,
            second.Agent.Id,
            second.Runtime.Id,
            "Validate invoice total.",
            first.Clock.UtcNow,
            new ResourceReference(
                ResourceKind.WorkItem,
                workItem.Id.Value));

        Assert.True(request.IsSuccess, request.Error?.Message);

        var submitted = first.Runtime.Work.Delegation.Submit(
            first.Context,
            request.Value!);

        Assert.True(submitted.IsSuccess, submitted.Error?.Message);
        Assert.Equal(
            first.Context.PrincipalId!.Value,
            submitted.Value!.Provenance.CreatedBy);
        Assert.Equal(
            workItem.Id.Value,
            submitted.Value.Source!.Value.Identity);

        var delegateRead = second.Runtime.Work.Delegation.Read(
            second.Context,
            second.Agent.Id,
            second.Runtime.Id,
            submitted.Value.Id);

        Assert.True(delegateRead.IsSuccess, delegateRead.Error?.Message);

        var outsiderRead = outsider.Runtime.Work.Delegation.Read(
            outsider.Context,
            outsider.Agent.Id,
            outsider.Runtime.Id,
            submitted.Value.Id);

        Assert.True(outsiderRead.IsFailure);
        Assert.Equal(
            "hive.agent.delegation.not-authorized",
            outsiderRead.Error!.Code);
    }

    [Fact]
    public void RuntimeWorkProtocols_AreIndependentAndStopPreservesOwnState()
    {
        var fixture = CreateRuntime();
        var second = CreateRuntime(
            fixture.DeploymentId,
            fixture.TenantId,
            fixture.Context.PrincipalId!.Value);

        Assert.NotSame(fixture.Runtime.Work, second.Runtime.Work);
        Assert.NotSame(fixture.Runtime.Work.Memory, second.Runtime.Work.Memory);
        Assert.NotSame(fixture.Runtime.Work.Questions, second.Runtime.Work.Questions);
        Assert.NotSame(fixture.Runtime.Work.Delegation, second.Runtime.Work.Delegation);

        var memory = fixture.Runtime.Work.Memory.Store(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            "state",
            "runtime-one",
            fixture.Clock.UtcNow);

        Assert.True(memory.IsSuccess, memory.Error?.Message);

        var stopped = fixture.Runtime.Stop(fixture.Clock.UtcNow.AddMinutes(1));

        Assert.True(stopped.IsSuccess, stopped.Error?.Message);
        Assert.Same(fixture.Runtime.Work, stopped.Value!.Work);

        var afterStop = stopped.Value.Work.Memory.Retrieve(
            fixture.Context,
            fixture.Agent.Id,
            fixture.Runtime.Id,
            "state");

        Assert.True(afterStop.IsSuccess, afterStop.Error?.Message);
        Assert.Single(afterStop.Value!);
    }

    private static RuntimeFixture CreateRuntime(
        DeploymentId? deploymentId = null,
        TenantId? tenantId = null,
        PrincipalId? principalId = null,
        IQuestionTransport? questions = null,
        IDelegationChannel? delegation = null)
    {
        var deployment = deploymentId ?? DeploymentId.New();
        var tenant = tenantId ?? TenantId.New();
        var principal = principalId ?? PrincipalId.New();
        var now = new DateTimeOffset(2026, 9, 22, 6, 0, 0, TimeSpan.Zero);
        var clock = new FakeClock(now);
        var context = new ResourceAccessContext(
            deployment,
            tenant,
            principal);

        var agentResult = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition("protocol-agent", "Protocol Agent"),
                new AgentCreationContext(context));

        Assert.True(agentResult.IsSuccess, agentResult.Error?.Message);

        var runtime = agentResult.Value!.CreateRuntimeInstance(
            now,
            clock,
            questions,
            delegation);

        var runtimeContext = new ResourceAccessContext(
            deployment,
            tenant,
            principal,
            AgentId: agentResult.Value.Id,
            RuntimeId: runtime.Id);

        return new RuntimeFixture(
            agentResult.Value,
            runtime,
            runtimeContext,
            clock,
            deployment,
            tenant);
    }

    private static WorkItem CreateWorkItem(RuntimeFixture fixture) =>
        WorkItem.Create(
            WorkItemId.New(),
            fixture.Context.PrincipalId!.Value,
            ResourceScope.Runtime(fixture.Runtime.Id),
            new ResourceProvenance(
                fixture.Context.PrincipalId.Value,
                fixture.Clock.UtcNow,
                CorrelationId.New()),
            fixture.Clock.UtcNow);

    private sealed record RuntimeFixture(
        Agent Agent,
        RuntimeInstance Runtime,
        ResourceAccessContext Context,
        FakeClock Clock,
        DeploymentId DeploymentId,
        TenantId TenantId);
}
