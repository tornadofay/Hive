using Hive.Core;

namespace Hive.Agents;

public sealed class RuntimeWorkProtocols
{
    public RuntimeWorkProtocols(
        AgentId agentId,
        RuntimeId runtimeId,
        IClock? clock = null)
    {
        AgentId = agentId;
        RuntimeId = runtimeId;

        Objectives = new ObjectiveStore();
        Memory = new AgentMemoryStore();
        Questions = new QuestionTransport(clock);
        UnderstandingGate = new UnderstandingGate();
        Delegation = new InMemoryDelegationChannel();
    }

    public AgentId AgentId { get; }

    public RuntimeId RuntimeId { get; }

    public ObjectiveStore Objectives { get; }

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
