using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;

namespace Hive.Example.WinForms;

internal sealed class BaseAgentWorkProtocolsExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public BaseAgentWorkProtocolsExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run work protocol example"
        };

        _surface.SetInformation(
            "Creates one base Agent with two independent RuntimeInstance objects and exercises Objective, WorkItem binding, memory, Question/Answer, the deterministic Understanding Gate, and explicit cross-runtime delegation.",
            "Each runtime gets independent Objective, Memory, and Question state. Delegation is cross-runtime only when the same explicit IDelegationChannel is shared. No persistence, MAF workflow, provider call, or cognitive strategy is involved.",
            "Scope",
            "Base Agent mechanisms only; generation remains Base");

        _surface.CodeSnippet = """
            var agent = new AgentFactory(authorizer)
                .Create<Agent>(definition, context);

            var runtime = agent.Value.CreateRuntimeInstance();
            var objective = runtime.Work.Objectives.Create(
                runtimeContext,
                agent.Value.Id,
                runtime.Id,
                "Prepare invoice",
                new ObjectiveUpdate("Required data is known.", 10, null, []),
                DateTimeOffset.UtcNow);
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

        var now = new DateTimeOffset(
            2026,
            9,
            22,
            6,
            0,
            0,
            TimeSpan.Zero);

        var clock = new ExampleClock(now);
        var delegation = new InMemoryDelegationChannel();

        var principal = PrincipalId.New();
        var deployment = DeploymentId.New();
        var tenant = TenantId.New();

        var creationContext = new AgentCreationContext(
            new ResourceAccessContext(
                deployment,
                tenant,
                principal));

        var agentResult = new AgentFactory(
            new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition(
                    "example-work-protocol-agent",
                    "Example Work Protocol Agent",
                    AgentGeneration.Base),
                creationContext);

        EnsureSuccess(agentResult, "Agent creation");

        var agent = agentResult.Value!;

        var firstRuntime = agent.CreateRuntimeInstance(
            now,
            clock,
            delegation: delegation);
        var secondRuntime = agent.CreateRuntimeInstance(
            now,
            clock,
            delegation: delegation);

        var firstContext = new ResourceAccessContext(
            deployment,
            tenant,
            principal,
            AgentId: agent.Id,
            RuntimeId: firstRuntime.Id);

        var secondContext = new ResourceAccessContext(
            deployment,
            tenant,
            principal,
            AgentId: agent.Id,
            RuntimeId: secondRuntime.Id);

        var workItem = WorkItem.Create(
            WorkItemId.New(),
            principal,
            ResourceScope.Runtime(firstRuntime.Id),
            new ResourceProvenance(
                principal,
                clock.UtcNow,
                CorrelationId.New()),
            clock.UtcNow);

        var binding = firstRuntime.Work.BindWorkItem(
            firstContext,
            workItem,
            clock.UtcNow,
            CorrelationId.New());

        EnsureSuccess(binding, "WorkItem binding");

        var objectiveResult = firstRuntime.Work.Objectives.Create(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            "Prepare invoice",
            new ObjectiveUpdate(
                "Customer and amount are validated.",
                10,
                clock.UtcNow.AddHours(1),
                []),
            clock.UtcNow,
            binding.Value);

        EnsureSuccess(objectiveResult, "Objective creation");

        var updatedObjective = firstRuntime.Work.Objectives.Update(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            objectiveResult.Value!.Id,
            new ObjectiveUpdate(
                "Customer, amount, and tax are validated.",
                20,
                clock.UtcNow.AddHours(2),
                []),
            clock.UtcNow.AddMinutes(1));

        EnsureSuccess(updatedObjective, "Objective update");

        var completedObjective = firstRuntime.Work.Objectives.Complete(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            objectiveResult.Value.Id,
            clock.UtcNow.AddMinutes(2));

        EnsureSuccess(completedObjective, "Objective completion");

        var memory = firstRuntime.Work.Memory.Store(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            "customer.name",
            "Alice",
            clock.UtcNow);

        EnsureSuccess(memory, "Memory store");

        var firstMemoryRead = firstRuntime.Work.Memory.Retrieve(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            "customer.name");

        EnsureSuccess(firstMemoryRead, "First runtime memory retrieval");

        var secondMemoryRead = secondRuntime.Work.Memory.Retrieve(
            secondContext,
            agent.Id,
            secondRuntime.Id,
            "customer.name");

        EnsureSuccess(secondMemoryRead, "Second runtime memory retrieval");

        var question = firstRuntime.Work.Questions.Ask(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            "Is the invoice ready to write?",
            TimeSpan.FromMinutes(5),
            clock.UtcNow);

        EnsureSuccess(question, "Question creation");

        var answered = firstRuntime.Work.Questions.Answer(
            question.Value!.Id,
            agent.Id,
            firstRuntime.Id,
            "Yes",
            clock.UtcNow.AddMinutes(1));

        EnsureSuccess(answered, "Question answer");

        var waited = await firstRuntime.Work.Questions.WaitAsync(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            question.Value.Id,
            cancellationToken);

        EnsureSuccess(waited, "Question wait");

        var timeoutQuestion = firstRuntime.Work.Questions.Ask(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            "Will another approval arrive?",
            TimeSpan.FromMinutes(5),
            clock.UtcNow);

        EnsureSuccess(timeoutQuestion, "Timeout question creation");

        clock.Advance(TimeSpan.FromMinutes(5));

        var expired = firstRuntime.Work.Questions.ExpireDue();
        if (expired != 1)
        {
            throw new InvalidOperationException(
                $"Expected one expired Question but observed {expired}.");
        }

        var timedOut = await firstRuntime.Work.Questions.WaitAsync(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            timeoutQuestion.Value!.Id,
            cancellationToken);

        EnsureSuccess(timedOut, "Timed-out Question wait");

        var gate = new UnderstandingGate();
        var policy = new UnderstandingGatePolicy(
            ["customer.name", "invoice.total"],
            confirmationRequired: true);

        var blocked = gate.Evaluate(
            policy,
            ["customer.name"],
            confirmationReceived: false);

        var satisfied = gate.Evaluate(
            policy,
            ["customer.name", "invoice.total"],
            confirmationReceived: true);

        var delegationRequest = DelegationRequest.Create(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            agent.Id,
            secondRuntime.Id,
            "Validate invoice total.",
            clock.UtcNow,
            new ResourceReference(
                ResourceKind.WorkItem,
                workItem.Id.Value));

        EnsureSuccess(delegationRequest, "Delegation creation");

        var submittedDelegation = firstRuntime.Work.Delegation.Submit(
            firstContext,
            delegationRequest.Value!);

        EnsureSuccess(submittedDelegation, "Delegation submission");

        var delegateRead = secondRuntime.Work.Delegation.Read(
            secondContext,
            agent.Id,
            secondRuntime.Id,
            submittedDelegation.Value!.Id);

        EnsureSuccess(delegateRead, "Delegation read");

        var stoppedRuntime = firstRuntime.Stop(clock.UtcNow);

        EnsureSuccess(stoppedRuntime, "Runtime stop");

        var stoppedMemoryRead = stoppedRuntime.Value!.Work.Memory.Retrieve(
            firstContext,
            agent.Id,
            firstRuntime.Id,
            "customer.name");

        EnsureSuccess(stoppedMemoryRead, "Stopped-runtime memory inspection");

        _output.Write(
            "Base Agent Work Protocols",
            $"""
            Agent: {agent.Id}; generation={agent.Generation}
            Runtime 1: {firstRuntime.Id}
            Runtime 2: {secondRuntime.Id}
            WorkItem: {workItem.Id}; bindingVersion={binding.Value!.WorkItemVersion}
            Objective: {completedObjective.Value!.Id}; final={completedObjective.Value.Status}
            Memory runtime 1 count: {firstMemoryRead.Value!.Count}
            Memory runtime 2 count: {secondMemoryRead.Value!.Count}
            Question: {waited.Value!.Id}; final={waited.Value.Status}; answer={waited.Value.Answer}
            Timeout Question: {timedOut.Value!.Id}; final={timedOut.Value.Status}
            Understanding Gate: blocked={blocked.Status}; missing={string.Join(", ", blocked.MissingInformation)}
            Understanding Gate after confirmation: {satisfied.Status}
            Delegation: {submittedDelegation.Value!.Id}; delegate runtime={delegateRead.Value!.DelegateRuntimeId}
            Delegation provenance: {submittedDelegation.Value.Provenance.CreatedBy}
            Stopped runtime state preserved: {stoppedMemoryRead.Value!.Count == 1}
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
        public ExampleClock(DateTimeOffset utcNow)
        {
            UtcNow = utcNow.ToUniversalTime();
        }

        public DateTimeOffset UtcNow { get; private set; }

        public void Advance(TimeSpan amount)
        {
            if (amount < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount),
                    amount,
                    "Clock cannot move backwards.");
            }

            UtcNow = UtcNow.Add(amount);
        }
    }
}
