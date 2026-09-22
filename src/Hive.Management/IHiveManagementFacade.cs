using Hive.Agents;
using Hive.Core;

namespace Hive.Management;

public interface IHiveManagementFacade
{
    Task<Result<Provider>> CreateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<Provider>> GetProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<Result<Provider>> UpdateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<Provider>> DeleteProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderAccount>> CreateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderAccount>> GetProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderAccount>> UpdateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<ProviderAccount>> DeleteProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<ExecutionTarget>> GetExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

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

    Task<Result<WorkItem>> CreateImageWorkItemAsync(
        WorkItemImageSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> GetWorkItemAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkItem>>> ListWorkItemsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItemAttachmentContent>> GetWorkItemAttachmentAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<WorkItemActivity>>> GetWorkItemActivityAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> RequestWorkItemApprovalAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> ApproveWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default);

    Task<Result<WorkItem>> RejectWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string reason,
        CancellationToken cancellationToken = default);

}
