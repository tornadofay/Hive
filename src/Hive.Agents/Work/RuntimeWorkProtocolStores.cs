namespace Hive.Agents;

public sealed class RuntimeWorkProtocolStores
{
    public RuntimeWorkProtocolStores(
        IObjectiveStore objectives,
        IAgentMemoryStore memory,
        IQuestionTransport questions,
        IDelegationChannel delegation)
    {
        Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        Questions = questions ?? throw new ArgumentNullException(nameof(questions));
        Delegation = delegation ?? throw new ArgumentNullException(nameof(delegation));
    }

    public IObjectiveStore Objectives { get; }

    public IAgentMemoryStore Memory { get; }

    public IQuestionTransport Questions { get; }

    public IDelegationChannel Delegation { get; }
}
