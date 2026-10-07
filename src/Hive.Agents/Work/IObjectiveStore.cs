using Hive.Core;

namespace Hive.Agents;

public interface IObjectiveStore
{
    Result<Objective> Create(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string title,
        ObjectiveUpdate state,
        DateTimeOffset createdAtUtc,
        WorkItemBinding? workItemBinding = null);

    Result<Objective> Get(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId);

    Result<Objective> Update(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        ObjectiveUpdate update,
        DateTimeOffset changedAtUtc);

    Result<Objective> BindWorkItem(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        WorkItemBinding binding,
        DateTimeOffset changedAtUtc);

    Result<Objective> Complete(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        DateTimeOffset completedAtUtc);

    Result<Objective> Cancel(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        ObjectiveId objectiveId,
        DateTimeOffset cancelledAtUtc);
}
