using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

public sealed class HiveManagementFacade : IHiveManagementFacade
{
    private readonly HiveConfigurationManagementService _configuration;
    private readonly HiveSecretManagementService _secrets;
    private readonly HiveProviderManagementService _providers;
    private readonly HiveAgentManagementService _agents;
    private readonly HiveWorkItemManagementService _workItems;

    public HiveManagementFacade(
        IProviderResourceStore providerResources,
        IAgentDefinitionResourceStore agentDefinitions,
        IWorkItemResourceStore workItems,
        ISecretStore? secrets = null,
        IProviderConnectionTester? providerConnectionTester = null,
        IHiveConfigurationStore? configurationStore = null,
        IHivePersistenceConnectionTester? persistenceConnectionTester = null,
        IHiveBootstrapCredentialStore? bootstrapCredentials = null,
        AgentExecutionService? agentExecution = null)
    {
        _configuration = new HiveConfigurationManagementService(
            configurationStore,
            persistenceConnectionTester,
            bootstrapCredentials);
        _secrets = new HiveSecretManagementService(secrets);
        _providers = new HiveProviderManagementService(
            providerResources,
            providerConnectionTester,
            secrets);
        _agents = new HiveAgentManagementService(
            agentDefinitions,
            providerResources,
            secrets,
            agentExecution);
        _workItems = new HiveWorkItemManagementService(workItems);
    }

    public Task<Result<HivePersistenceConfiguration>> GetPersistenceConfigurationAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _configuration.GetPersistenceConfigurationAsync(accessContext, cancellationToken);

    public Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _configuration.SavePersistenceConfigurationAsync(configuration, accessContext, cancellationToken);

    public Task<Result<HiveBootstrapCredentialReference>> SaveBootstrapCredentialAsync(
        SecretMaterial material,
        HiveBootstrapCredentialReference? existingReference,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _configuration.SaveBootstrapCredentialAsync(material, existingReference, accessContext, cancellationToken);

    public Task<Result> RemoveBootstrapCredentialAsync(
        HiveBootstrapCredentialReference reference,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _configuration.RemoveBootstrapCredentialAsync(reference, accessContext, cancellationToken);

    public Task<Result<HivePersistenceConnectionTest>> TestPersistenceConnectionAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _configuration.TestPersistenceConnectionAsync(configuration, accessContext, cancellationToken);

    public Task<Result> InitializePersistenceAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _configuration.InitializePersistenceAsync(configuration, accessContext, cancellationToken);

    public Task<Result<Secret>> CreateSecretAsync(
        string key,
        string displayName,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _secrets.CreateSecretAsync(key, displayName, material, accessContext, cancellationToken);

    public Task<Result<Secret>> GetSecretDescriptorAsync(
        SecretId secretId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _secrets.GetSecretDescriptorAsync(secretId, accessContext, cancellationToken);

    public Task<Result<Secret>> ReplaceSecretAsync(
        SecretId secretId,
        SecretMaterial replacement,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _secrets.ReplaceSecretAsync(secretId, replacement, expectedVersion, accessContext, cancellationToken);

    public Task<Result<ProviderConnectionTestResult>> TestExecutionTargetConnectionAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.TestExecutionTargetConnectionAsync(executionTargetId, accessContext, cancellationToken);

    public Task<Result<Provider>> CreateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.CreateProviderAsync(provider, accessContext, cancellationToken);

    public Task<Result<Provider>> GetProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.GetProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        _providers.ListProvidersAsync(accessContext, includeRetired, cancellationToken);

    public Task<Result<Provider>> UpdateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.UpdateProviderAsync(provider, accessContext, cancellationToken);

    public Task<Result<Provider>> DeleteProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.DeleteProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<Provider>> ReactivateProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.ReactivateProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> CreateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.CreateProviderAccountAsync(account, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> GetProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.GetProviderAccountAsync(providerAccountId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        _providers.ListProviderAccountsAsync(providerId, accessContext, includeRetired, cancellationToken);

    public Task<Result<ProviderAccount>> UpdateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.UpdateProviderAccountAsync(account, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> DeleteProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.DeleteProviderAccountAsync(providerAccountId, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> ReactivateProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.ReactivateProviderAccountAsync(providerAccountId, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.CreateExecutionTargetAsync(target, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> GetExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.GetExecutionTargetAsync(executionTargetId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        _providers.ListExecutionTargetsAsync(providerAccountId, accessContext, includeRetired, cancellationToken);

    public Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.UpdateExecutionTargetAsync(target, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.DeleteExecutionTargetAsync(executionTargetId, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> ReactivateExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _providers.ReactivateExecutionTargetAsync(executionTargetId, accessContext, cancellationToken);

    public Task<Result<AgentDefinition>> CreateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _agents.CreateAgentDefinitionAsync(definition, accessContext, cancellationToken);

    public Task<Result<AgentDefinition>> GetAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _agents.GetAgentDefinitionAsync(agentDefinitionId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<AgentDefinition>>> ListAgentDefinitionsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        _agents.ListAgentDefinitionsAsync(accessContext, includeRetired, cancellationToken);

    public Task<Result<AgentDefinition>> UpdateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _agents.UpdateAgentDefinitionAsync(definition, accessContext, cancellationToken);

    public Task<Result<AgentDefinition>> DeleteAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _agents.DeleteAgentDefinitionAsync(agentDefinitionId, accessContext, cancellationToken);

    public Task<Result<AgentDefinition>> ReactivateAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _agents.ReactivateAgentDefinitionAsync(agentDefinitionId, accessContext, cancellationToken);

    public Task<Result<AgentExecutionResult>> ExecuteConfiguredAgentAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        string userMessage,
        CancellationToken cancellationToken = default) =>
        _agents.ExecuteConfiguredAgentAsync(agentDefinitionId, accessContext, userMessage, cancellationToken);

    public Task<Result<WorkItem>> CreateImageWorkItemAsync(
        WorkItemImageSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _workItems.CreateImageWorkItemAsync(submission, accessContext, cancellationToken);

    public Task<Result<WorkItem>> GetWorkItemAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _workItems.GetWorkItemAsync(workItemId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<WorkItem>>> ListWorkItemsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        _workItems.ListWorkItemsAsync(accessContext, includeRetired, cancellationToken);

    public Task<Result<WorkItemAttachmentContent>> GetWorkItemAttachmentAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _workItems.GetWorkItemAttachmentAsync(workItemId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<WorkItemActivity>>> GetWorkItemActivityAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _workItems.GetWorkItemActivityAsync(workItemId, accessContext, cancellationToken);

    public Task<Result<WorkItem>> RequestWorkItemApprovalAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _workItems.RequestWorkItemApprovalAsync(workItemId, expectedVersion, accessContext, cancellationToken);

    public Task<Result<WorkItem>> ApproveWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        _workItems.ApproveWorkItemAsync(workItemId, expectedVersion, accessContext, cancellationToken);

    public Task<Result<WorkItem>> RejectWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string reason,
        CancellationToken cancellationToken = default) =>
        _workItems.RejectWorkItemAsync(workItemId, expectedVersion, accessContext, reason, cancellationToken);
}