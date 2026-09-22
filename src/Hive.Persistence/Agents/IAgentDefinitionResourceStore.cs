using Hive.Agents;
using Hive.Core;

namespace Hive.Persistence;

public interface IAgentDefinitionResourceStore
{
    Task<Result<AgentDefinition>> CreateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<AgentDefinition>> GetAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<AgentDefinition>>> ListAgentDefinitionsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<Result<AgentDefinition>> UpdateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<AgentDefinition>> DeleteAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);
}
