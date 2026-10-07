using Hive.Core;

namespace Hive.Agents;

public sealed class RuntimeWorkProtocols
{
    internal RuntimeWorkProtocols(
        AgentId agentId,
        RuntimeId runtimeId,
        IClock? clock = null,
        IDelegationChannel? delegation = null,
        RuntimeWorkProtocolStores? stores = null)
    {
        AgentId = agentId;
        RuntimeId = runtimeId;

        var effectiveClock = clock ?? SystemClock.Instance;
        var effectiveStores = stores ??
            new RuntimeWorkProtocolStores(
                new ObjectiveStore(),
                new AgentMemoryStore(),
                new QuestionTransport(effectiveClock),
                delegation ?? new InMemoryDelegationChannel());

        Objectives = effectiveStores.Objectives;
        Memory = effectiveStores.Memory;
        Questions = effectiveStores.Questions;
        UnderstandingGate = new UnderstandingGate();
        Delegation = delegation ?? effectiveStores.Delegation;
    }

    public AgentId AgentId { get; }

    public RuntimeId RuntimeId { get; }

    public IObjectiveStore Objectives { get; }

    public IAgentMemoryStore Memory { get; }

    public IQuestionTransport Questions { get; }

    public IUnderstandingGate UnderstandingGate { get; }

    public IDelegationChannel Delegation { get; }

    public Result<WorkItemBinding> BindWorkItem(
        ResourceAccessContext accessContext,
        WorkItem workItem,
        DateTimeOffset boundAtUtc,
        CorrelationId correlationId,
        CausationId? causationId = null)
    {
        return WorkItemBinding.Create(
            workItem,
            accessContext,
            AgentId,
            RuntimeId,
            boundAtUtc,
            correlationId,
            causationId);
    }
}
