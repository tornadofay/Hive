using Hive.Agents;

using Hive.Core;

using Hive.Persistence;



namespace Hive.Management;



internal sealed class HiveAgentManagementService : HiveManagementServiceBase

{

    private readonly IAgentDefinitionResourceStore _agentDefinitions;

    private readonly IProviderResourceStore _providerResources;

    private readonly ISecretStore? _secrets;

    private readonly AgentExecutionService? _agentExecution;



    internal HiveAgentManagementService(IAgentDefinitionResourceStore agentDefinitions, IProviderResourceStore providerResources, ISecretStore? secrets, AgentExecutionService? agentExecution)

    {

        _agentDefinitions = agentDefinitions ?? throw new ArgumentNullException(nameof(agentDefinitions));

        _providerResources = providerResources ?? throw new ArgumentNullException(nameof(providerResources));

        _secrets = secrets;

        _agentExecution = agentExecution;

    }



    internal Task<Result<AgentDefinition>> CreateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        SaveAgentDefinitionAsync(
            definition,
            accessContext,
            isCreate: true,
            cancellationToken);


    internal Task<Result<AgentDefinition>> GetAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            agentDefinitionId == default,
            accessContext,
            "agent definition",
            () => _agentDefinitions.GetAgentDefinitionAsync(
                agentDefinitionId,
                accessContext,
                cancellationToken));


    internal Task<Result<IReadOnlyList<AgentDefinition>>> ListAgentDefinitionsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            accessContext,
            "agent definition",
            () => _agentDefinitions.ListAgentDefinitionsAsync(
                accessContext,
                includeRetired,
                cancellationToken));


    internal Task<Result<AgentDefinition>> UpdateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        SaveAgentDefinitionAsync(
            definition,
            accessContext,
            isCreate: false,
            cancellationToken);


    internal Task<Result<AgentDefinition>> DeleteAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            agentDefinitionId == default,
            accessContext,
            "agent definition",
            () => _agentDefinitions.DeleteAgentDefinitionAsync(
                agentDefinitionId,
                accessContext,
                cancellationToken));


    internal async Task<Result<AgentDefinition>> ReactivateAgentDefinitionAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<AgentDefinition>.Failure(contextError);

        if (agentDefinitionId == default)
        {
            return Result<AgentDefinition>.Failure(
                Error.Validation(
                    "hive.management.agent-definition.identity-required",
                    "The agent definition identity is required."));
        }

        var current = await _agentDefinitions
            .GetAgentDefinitionAsync(
                agentDefinitionId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (current.IsFailure)
            return Result<AgentDefinition>.Failure(current.Error!);

        if (current.Value!.Resource!.Lifecycle.Status == ResourceLifecycleStatus.Active)
            return current;

        if (current.Value.Resource.Lifecycle.Status != ResourceLifecycleStatus.Retired)
        {
            return Result<AgentDefinition>.Failure(
                Error.Conflict(
                    "hive.management.agent-definition.lifecycle-invalid",
                    "Only a retired AgentDefinition can be reactivated."));
        }

        var targetValidation = await ValidateConfiguredExecutionTargetAsync(
            current.Value.ConfiguredExecutionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (targetValidation.IsFailure)
            return Result<AgentDefinition>.Failure(targetValidation.Error!);

        return await _agentDefinitions
            .ReactivateAgentDefinitionAsync(
                agentDefinitionId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);
    }


    internal async Task<Result<AgentDefinition>> SaveAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        bool isCreate,
        CancellationToken cancellationToken)
    {
        if (definition is null)
        {
            return Result<AgentDefinition>.Failure(
                Error.Validation(
                    "hive.management.agent-definition.resource-required",
                    "An agent definition is required."));
        }

        var validation = ValidateResource(
            definition.Resource,
            ResourceKind.AgentDefinition,
            accessContext,
            "agent definition",
            isCreate);

        if (validation is not null)
            return Result<AgentDefinition>.Failure(validation);

        var targetValidation = await ValidateConfiguredExecutionTargetAsync(
            definition.ConfiguredExecutionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (targetValidation.IsFailure)
            return Result<AgentDefinition>.Failure(targetValidation.Error!);

        return isCreate
            ? await _agentDefinitions.CreateAgentDefinitionAsync(
                definition,
                accessContext,
                cancellationToken).ConfigureAwait(false)
            : await _agentDefinitions.UpdateAgentDefinitionAsync(
                definition,
                accessContext,
                cancellationToken).ConfigureAwait(false);
    }


    internal async Task<Result> ValidateConfiguredExecutionTargetAsync(
        ExecutionTargetId? executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (executionTargetId is null)
            return Result.Success();

        if (executionTargetId.Value == default)
        {
            return Result.Failure(
                Error.Validation(
                    "hive.management.agent-definition.execution-target-invalid",
                    "A configured execution target reference must contain a valid target identity."));
        }

        var target = await _providerResources.GetExecutionTargetAsync(
            executionTargetId.Value,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (target.IsFailure)
            return Result.Failure(target.Error!);

        var targetValue = target.Value!;

        if (targetValue.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.management.agent-definition.execution-target-retired",
                    "A retired execution target cannot be configured for an AgentDefinition."));
        }

        if (targetValue.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.management.agent-definition.execution-target-inactive",
                    "An inactive execution target cannot be configured for an AgentDefinition."));
        }

        var account = await _providerResources.GetProviderAccountAsync(
            targetValue.ProviderAccountId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (account.IsFailure)
            return Result.Failure(account.Error!);

        var accountValue = account.Value!;

        if (accountValue.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.management.agent-definition.execution-target-account-inactive",
                    "An AgentDefinition cannot be configured with an execution target whose ProviderAccount is not active."));
        }

        var provider = await _providerResources.GetProviderAsync(
            targetValue.ProviderId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (provider.IsFailure)
            return Result.Failure(provider.Error!);

        var providerValue = provider.Value!;

        if (providerValue.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.management.agent-definition.execution-target-provider-inactive",
                    "An AgentDefinition cannot be configured with an execution target whose Provider is not active."));
        }

        if (accountValue.ProviderId != providerValue.Id)
        {
            return Result.Failure(
                Error.Conflict(
                    "hive.management.agent-definition.execution-target-provider-mismatch",
                    "The execution target references a ProviderAccount owned by a different Provider."));
        }

        return Result.Success();
    }


    internal async Task<Result<AgentExecutionResult>> ExecuteConfiguredAgentAsync(
        AgentDefinitionId agentDefinitionId,
        ResourceAccessContext accessContext,
        string userMessage,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<AgentExecutionResult>.Failure(contextError);

        if (agentDefinitionId == default)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Validation(
                    "hive.management.agent-definition.identity-required",
                    "An AgentDefinition identity is required."));
        }

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Validation(
                    "hive.management.agent-execution.message-required",
                    "An Agent execution message is required."));
        }

        if (_agentExecution is null)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Unsupported(
                    "hive.management.agent-execution-unavailable",
                    "Configured Agent execution is not available in the current host service graph."));
        }

        var definition = await _agentDefinitions
            .GetAgentDefinitionAsync(
                agentDefinitionId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (definition.IsFailure)
            return Result<AgentExecutionResult>.Failure(definition.Error!);

        if (definition.Value!.Resource!.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Unsupported(
                    "hive.management.agent-execution.agent-definition-inactive",
                    "The configured AgentDefinition is not active."));
        }

        if (definition.Value.ConfiguredExecutionTargetId is null)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Validation(
                    "hive.management.agent-definition.execution-target-required",
                    $"AgentDefinition '{definition.Value.Id}' does not have a configured ExecutionTarget."));
        }

        var target = await _providerResources
            .GetExecutionTargetAsync(
                definition.Value.ConfiguredExecutionTargetId.Value,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (target.IsFailure)
            return Result<AgentExecutionResult>.Failure(target.Error!);

        if (target.Value!.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Unsupported(
                    "hive.management.agent-execution.execution-target-inactive",
                    "The configured ExecutionTarget is not active."));
        }

        var provider = await _providerResources
            .GetProviderAsync(
                target.Value.ProviderId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (provider.IsFailure)
            return Result<AgentExecutionResult>.Failure(provider.Error!);

        if (provider.Value!.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Unsupported(
                    "hive.management.agent-execution.provider-inactive",
                    "The configured Provider is not active."));
        }

        var account = await _providerResources
            .GetProviderAccountAsync(
                target.Value.ProviderAccountId,
                accessContext,
                cancellationToken)
            .ConfigureAwait(false);

        if (account.IsFailure)
            return Result<AgentExecutionResult>.Failure(account.Error!);

        if (account.Value!.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Unsupported(
                    "hive.management.agent-execution.provider-account-inactive",
                    "The configured ProviderAccount is not active."));
        }

        if (account.Value.ProviderId != provider.Value.Id)
        {
            return Result<AgentExecutionResult>.Failure(
                Error.Conflict(
                    "hive.management.execution-target-provider-mismatch",
                    "The configured ExecutionTarget references a ProviderAccount owned by a different Provider."));
        }

        SecretMaterial? credential = null;

        try
        {
            if (account.Value.CredentialSecret is not null)
            {
                if (_secrets is null)
                {
                    return Result<AgentExecutionResult>.Failure(
                        Error.Unsupported(
                            "hive.management.secret-store-unavailable",
                            "The Hive Secret Store is not configured."));
                }

                var secret = await _secrets
                    .GetAsync(
                        account.Value.CredentialSecret.Value.Id,
                        accessContext,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (secret.IsFailure)
                    return Result<AgentExecutionResult>.Failure(secret.Error!);

                credential = secret.Value!.Material;
            }

            var agentResult = new AgentFactory(
                    new AllowBaseAgentCreationAuthorizer())
                .Create<Agent>(
                    definition.Value,
                    new AgentCreationContext(accessContext));

            if (agentResult.IsFailure)
                return Result<AgentExecutionResult>.Failure(agentResult.Error!);

            var agent = agentResult.Value!;
            var runtime = agent.CreateRuntimeInstance();

            var result = await _agentExecution
                .ExecuteAsync(
                    new AgentExecutionRequest(
                        agent,
                        runtime,
                        target.Value,
                        accessContext,
                        userMessage,
                        credential),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.IsFailure &&
                result.Error!.Category == ErrorCategory.Internal)
            {
                return Result<AgentExecutionResult>.Failure(
                    SanitizeTechnicalError(
                        result.Error,
                        "The configured Agent execution failed unexpectedly."));
            }

            return result;
        }
        finally
        {
            credential?.Dispose();
        }
    }


}