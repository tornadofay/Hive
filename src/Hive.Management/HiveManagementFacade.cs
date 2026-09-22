using Hive.Agents;
using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

public sealed class HiveManagementFacade : IHiveManagementFacade
{
    private readonly IProviderResourceStore _providerResources;
    private readonly IAgentDefinitionResourceStore _agentDefinitions;
    private readonly IWorkItemResourceStore _workItems;
    private readonly ISecretStore? _secrets;
    private readonly IProviderConnectionTester? _providerConnectionTester;
    private readonly IHiveConfigurationStore? _configurationStore;
    private readonly IHivePersistenceConnectionTester? _persistenceConnectionTester;

    public HiveManagementFacade(
        IProviderResourceStore providerResources,
        IAgentDefinitionResourceStore agentDefinitions,
        IWorkItemResourceStore workItems,
        ISecretStore? secrets = null,
        IProviderConnectionTester? providerConnectionTester = null,
        IHiveConfigurationStore? configurationStore = null,
        IHivePersistenceConnectionTester? persistenceConnectionTester = null)
    {
        _providerResources = providerResources
            ?? throw new ArgumentNullException(nameof(providerResources));
        _agentDefinitions = agentDefinitions
            ?? throw new ArgumentNullException(nameof(agentDefinitions));
        _workItems = workItems
            ?? throw new ArgumentNullException(nameof(workItems));
        _secrets = secrets;
        _providerConnectionTester = providerConnectionTester;
        _configurationStore = configurationStore;
        _persistenceConnectionTester = persistenceConnectionTester;
    }

    public async Task<Result<HivePersistenceConfiguration>> GetPersistenceConfigurationAsync(
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceConfiguration>.Failure(contextError);

        if (_configurationStore is null)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                Error.Unsupported(
                    "hive.management.configuration-store-unavailable",
                    "Hive persistence configuration storage is not configured."));
        }

        return await _configurationStore
            .LoadPersistenceConfigurationAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<HivePersistenceConfiguration>> SavePersistenceConfigurationAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceConfiguration>.Failure(contextError);

        if (_configurationStore is null)
        {
            return Result<HivePersistenceConfiguration>.Failure(
                Error.Unsupported(
                    "hive.management.configuration-store-unavailable",
                    "Hive persistence configuration storage is not configured."));
        }

        return await _configurationStore
            .SavePersistenceConfigurationAsync(
                configuration,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Result<HivePersistenceConnectionTest>> TestPersistenceConnectionAsync(
        HivePersistenceConfiguration configuration,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<HivePersistenceConnectionTest>.Failure(contextError);

        if (_persistenceConnectionTester is null)
        {
            return Result<HivePersistenceConnectionTest>.Failure(
                Error.Unsupported(
                    "hive.management.persistence-tester-unavailable",
                    "Hive persistence connection testing is not configured."));
        }

        SecretMaterial? material = null;

        try
        {
            if (configuration.AuthenticationMode == HiveSqlAuthenticationMode.SqlPassword)
            {
                if (configuration.CredentialSecret is null || _secrets is null)
                {
                    return Result<HivePersistenceConnectionTest>.Failure(
                        Error.Validation(
                            "hive.management.persistence-credential-required",
                            "SQL password authentication requires a configured Secret Store credential."));
                }

                var secret = await _secrets.GetAsync(
                    configuration.CredentialSecret.Value.Id,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (secret.IsFailure)
                    return Result<HivePersistenceConnectionTest>.Failure(secret.Error!);

                material = secret.Value!.Material;
            }

            return await _persistenceConnectionTester
                .TestAsync(
                    configuration,
                    material,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            material?.Dispose();
        }
    }

    public Task<Result<Secret>> CreateSecretAsync(
        string key,
        string displayName,
        SecretMaterial material,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(material);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Failure<Secret>(contextError);

        if (_secrets is null)
        {
            return Failure<Secret>(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        if (accessContext.TenantId is null)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret-tenant-required",
                    "A tenant identity is required to create a Hive secret."));
        }

        var now = DateTimeOffset.UtcNow;
        Secret secret;

        try
        {
            secret = new Secret(
                new ResourceEnvelope<SecretId>(
                    ResourceKind.Secret,
                    SecretId.New(),
                    accessContext.PrincipalId!.Value,
                    ResourceScope.Tenant(accessContext.TenantId.Value),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        accessContext.PrincipalId.Value,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                key,
                displayName);
        }
        catch (ArgumentException exception)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret-invalid",
                    exception.Message));
        }

        return _secrets.CreateAsync(
            secret,
            material,
            accessContext,
            cancellationToken);
    }

    public Task<Result<Secret>> GetSecretDescriptorAsync(
        SecretId secretId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Failure<Secret>(contextError);

        if (secretId == default)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret.identity-required",
                    "The secret identity is required."));
        }

        if (_secrets is null)
        {
            return Failure<Secret>(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        return _secrets.GetDescriptorAsync(
            secretId,
            accessContext,
            cancellationToken);
    }

    public Task<Result<Secret>> ReplaceSecretAsync(
        SecretId secretId,
        SecretMaterial replacement,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Failure<Secret>(contextError);

        if (secretId == default)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret.identity-required",
                    "The secret identity is required."));
        }

        if (expectedVersion.Value <= 0)
        {
            return Failure<Secret>(
                Error.Validation(
                    "hive.management.secret.version-invalid",
                    "A positive secret version is required."));
        }

        if (_secrets is null)
        {
            return Failure<Secret>(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        return _secrets.ReplaceAsync(
            secretId,
            replacement,
            accessContext,
            expectedVersion,
            cancellationToken);
    }

    public async Task<Result<ProviderConnectionTestResult>> TestExecutionTargetConnectionAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<ProviderConnectionTestResult>.Failure(contextError);

        if (executionTargetId == default)
        {
            return Result<ProviderConnectionTestResult>.Failure(
                Error.Validation(
                    "hive.management.execution-target.identity-required",
                    "The execution target identity is required."));
        }

        if (_providerConnectionTester is null)
        {
            return Result<ProviderConnectionTestResult>.Failure(
                Error.Unsupported(
                    "hive.management.provider-tester-unavailable",
                    "Provider connection testing is not configured."));
        }

        var target = await _providerResources.GetExecutionTargetAsync(
            executionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (target.IsFailure)
            return Result<ProviderConnectionTestResult>.Failure(target.Error!);

        var account = await _providerResources.GetProviderAccountAsync(
            target.Value!.ProviderAccountId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (account.IsFailure)
            return Result<ProviderConnectionTestResult>.Failure(account.Error!);

        var provider = await _providerResources.GetProviderAsync(
            target.Value.ProviderId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (provider.IsFailure)
            return Result<ProviderConnectionTestResult>.Failure(provider.Error!);

        SecretMaterial? material = null;

        try
        {
            if (account.Value!.CredentialSecret is not null)
            {
                if (_secrets is null)
                {
                    return Result<ProviderConnectionTestResult>.Failure(
                        Error.Unsupported(
                            "hive.management.secret-store-unavailable",
                            "The provider account references a credential but the Secret Store is not configured."));
                }

                var secret = await _secrets.GetAsync(
                    account.Value.CredentialSecret.Value.Id,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (secret.IsFailure)
                    return Result<ProviderConnectionTestResult>.Failure(secret.Error!);

                material = secret.Value!.Material;
            }

            return await _providerConnectionTester
                .TestAsync(
                    provider.Value!,
                    account.Value!,
                    target.Value,
                    material,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            material?.Dispose();
        }
    }

    public Task<Result<Provider>> CreateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
            return Failure<Provider>("provider", "resource-required");

        return Execute(
            provider.Resource,
            ResourceKind.Provider,
            accessContext,
            "provider",
            isCreate: true,
            () => _providerResources.CreateProviderAsync(
                provider,
                accessContext,
                cancellationToken));
    }

    public Task<Result<Provider>> GetProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            providerId == default,
            accessContext,
            "provider",
            () => _providerResources.GetProviderAsync(
                providerId,
                accessContext,
                cancellationToken));

    public Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            accessContext,
            "provider",
            () => _providerResources.ListProvidersAsync(
                accessContext,
                includeRetired,
                cancellationToken));

    public Task<Result<Provider>> UpdateProviderAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (provider is null)
            return Failure<Provider>("provider", "resource-required");

        return Execute(
            provider.Resource,
            ResourceKind.Provider,
            accessContext,
            "provider",
            isCreate: false,
            () => _providerResources.UpdateProviderAsync(
                provider,
                accessContext,
                cancellationToken));
    }

    public Task<Result<Provider>> DeleteProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            providerId == default,
            accessContext,
            "provider",
            () => _providerResources.DeleteProviderAsync(
                providerId,
                accessContext,
                cancellationToken));

    public Task<Result<ProviderAccount>> CreateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (account is null)
            return Failure<ProviderAccount>("provider account", "resource-required");

        return Execute(
            account.Resource,
            ResourceKind.ProviderAccount,
            accessContext,
            "provider account",
            isCreate: true,
            () => _providerResources.CreateProviderAccountAsync(
                account,
                accessContext,
                cancellationToken));
    }

    public Task<Result<ProviderAccount>> GetProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            providerAccountId == default,
            accessContext,
            "provider account",
            () => _providerResources.GetProviderAccountAsync(
                providerAccountId,
                accessContext,
                cancellationToken));

    public Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            providerId == default,
            accessContext,
            "provider account",
            () => _providerResources.ListProviderAccountsAsync(
                providerId,
                accessContext,
                includeRetired,
                cancellationToken));

    public Task<Result<ProviderAccount>> UpdateProviderAccountAsync(
        ProviderAccount account,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (account is null)
            return Failure<ProviderAccount>("provider account", "resource-required");

        return Execute(
            account.Resource,
            ResourceKind.ProviderAccount,
            accessContext,
            "provider account",
            isCreate: false,
            () => _providerResources.UpdateProviderAccountAsync(
                account,
                accessContext,
                cancellationToken));
    }

    public Task<Result<ProviderAccount>> DeleteProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            providerAccountId == default,
            accessContext,
            "provider account",
            () => _providerResources.DeleteProviderAccountAsync(
                providerAccountId,
                accessContext,
                cancellationToken));

    public Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (target is null)
            return Failure<ExecutionTarget>("execution target", "resource-required");

        return Execute(
            target.Resource,
            ResourceKind.ExecutionTarget,
            accessContext,
            "execution target",
            isCreate: true,
            () => _providerResources.CreateExecutionTargetAsync(
                target,
                accessContext,
                cancellationToken));
    }

    public Task<Result<ExecutionTarget>> GetExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            executionTargetId == default,
            accessContext,
            "execution target",
            () => _providerResources.GetExecutionTargetAsync(
                executionTargetId,
                accessContext,
                cancellationToken));

    public Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            providerAccountId == default,
            accessContext,
            "execution target",
            () => _providerResources.ListExecutionTargetsAsync(
                providerAccountId,
                accessContext,
                includeRetired,
                cancellationToken));

    public Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(
        ExecutionTarget target,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (target is null)
            return Failure<ExecutionTarget>("execution target", "resource-required");

        return Execute(
            target.Resource,
            ResourceKind.ExecutionTarget,
            accessContext,
            "execution target",
            isCreate: false,
            () => _providerResources.UpdateExecutionTargetAsync(
                target,
                accessContext,
                cancellationToken));
    }

    public Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            executionTargetId == default,
            accessContext,
            "execution target",
            () => _providerResources.DeleteExecutionTargetAsync(
                executionTargetId,
                accessContext,
                cancellationToken));

    public Task<Result<AgentDefinition>> CreateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (definition is null)
            return Failure<AgentDefinition>("agent definition", "resource-required");

        return Execute(
            definition.Resource,
            ResourceKind.AgentDefinition,
            accessContext,
            "agent definition",
            isCreate: true,
            () => _agentDefinitions.CreateAgentDefinitionAsync(
                definition,
                accessContext,
                cancellationToken));
    }

    public Task<Result<AgentDefinition>> GetAgentDefinitionAsync(
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

    public Task<Result<IReadOnlyList<AgentDefinition>>> ListAgentDefinitionsAsync(
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

    public Task<Result<AgentDefinition>> UpdateAgentDefinitionAsync(
        AgentDefinition definition,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (definition is null)
            return Failure<AgentDefinition>("agent definition", "resource-required");

        return Execute(
            definition.Resource,
            ResourceKind.AgentDefinition,
            accessContext,
            "agent definition",
            isCreate: false,
            () => _agentDefinitions.UpdateAgentDefinitionAsync(
                definition,
                accessContext,
                cancellationToken));
    }

    public Task<Result<AgentDefinition>> DeleteAgentDefinitionAsync(
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

    public Task<Result<WorkItem>> CreateImageWorkItemAsync(
        WorkItemImageSubmission submission,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        if (submission is null)
            return Failure<WorkItem>(
                Error.Validation(
                    "hive.management.work-item.submission-required",
                    "An image WorkItem submission is required."));

        var contextError = ValidateAccessContext(accessContext);

        return contextError is null
            ? _workItems.CreateImageWorkItemAsync(
                submission,
                accessContext,
                cancellationToken)
            : Failure<WorkItem>(contextError);
    }

    public Task<Result<WorkItem>> GetWorkItemAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            workItemId == default,
            accessContext,
            "work item",
            () => _workItems.GetWorkItemAsync(
                workItemId,
                accessContext,
                cancellationToken));

    public Task<Result<IReadOnlyList<WorkItem>>> ListWorkItemsAsync(
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            accessContext,
            "work item",
            () => _workItems.ListWorkItemsAsync(
                accessContext,
                includeRetired,
                cancellationToken));

    public Task<Result<WorkItemAttachmentContent>> GetWorkItemAttachmentAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Get(
            workItemId == default,
            accessContext,
            "work item",
            () => _workItems.GetWorkItemAttachmentAsync(
                workItemId,
                accessContext,
                cancellationToken));

    public async Task<Result<IReadOnlyList<WorkItemActivity>>> GetWorkItemActivityAsync(
        WorkItemId workItemId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(contextError);

        if (workItemId == default)
        {
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(
                Error.Validation(
                    "hive.management.work-item.identity-required",
                    "The work item identity is required."));
        }

        var events = await _workItems.GetWorkItemActivityAsync(
            workItemId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (events.IsFailure)
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(events.Error!);

        try
        {
            var activities = new List<WorkItemActivity>(events.Value!.Count);

            foreach (var envelope in events.Value)
            {
                var version = ReadVersion(envelope);
                var status = ReadStatus(envelope);
                var reason = ReadString(envelope, "reason");

                activities.Add(
                    new WorkItemActivity(
                        envelope.EventId,
                        envelope.OccurredAtUtc,
                        version,
                        envelope.EventType.Value,
                        status,
                        ActivityMessage(envelope.EventType.Value, reason),
                        envelope.CorrelationId,
                        envelope.CausationId));
            }

            return Result<IReadOnlyList<WorkItemActivity>>.Success(activities);
        }
        catch (EventSerializationException exception)
        {
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(exception.Error);
        }
        catch (Exception exception)
        {
            return Result<IReadOnlyList<WorkItemActivity>>.Failure(
                new Error(
                    "hive.management.work-item.activity-invalid",
                    ErrorCategory.Serialization,
                    $"WorkItem activity could not be reconstructed: {exception.Message}"));
        }
    }

    public Task<Result<WorkItem>> RequestWorkItemApprovalAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        TransitionWorkItem(
            workItemId,
            expectedVersion,
            accessContext,
            "work item",
            () => _workItems.RequestApprovalAsync(
                workItemId,
                expectedVersion,
                accessContext,
                cancellationToken));

    public Task<Result<WorkItem>> ApproveWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        TransitionWorkItem(
            workItemId,
            expectedVersion,
            accessContext,
            "work item",
            () => _workItems.ApproveAsync(
                workItemId,
                expectedVersion,
                accessContext,
                cancellationToken));

    public Task<Result<WorkItem>> RejectWorkItemAsync(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var validation = ValidateTransitionArguments(
            workItemId,
            expectedVersion,
            accessContext,
            "work item");

        if (validation is not null)
            return Failure<WorkItem>(validation);

        if (string.IsNullOrWhiteSpace(reason))
        {
            return Failure<WorkItem>(
                Error.Validation(
                    "hive.management.work-item.rejection-reason-required",
                    "A rejection reason is required."));
        }

        var normalized = reason.Trim();

        if (normalized.Length > 2000)
        {
            return Failure<WorkItem>(
                Error.Validation(
                    "hive.management.work-item.rejection-reason-too-long",
                    "A WorkItem rejection reason cannot exceed 2000 characters."));
        }

        return _workItems.RejectAsync(
            workItemId,
            expectedVersion,
            accessContext,
            normalized,
            cancellationToken);
    }

    private static Task<Result<T>> Execute<TIdentity, T>(
        ResourceEnvelope<TIdentity>? resource,
        ResourceKind expectedKind,
        ResourceAccessContext accessContext,
        string resourceName,
        bool isCreate,
        Func<Task<Result<T>>> operation)
        where TIdentity : struct =>
        ValidateResource(
            resource,
            expectedKind,
            accessContext,
            resourceName,
            isCreate) is { } error
            ? Failure<T>(error)
            : operation();

    private static Task<Result<T>> Get<T>(
        bool invalidIdentity,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<T>>> operation) =>
        invalidIdentity
            ? Failure<T>(
                Error.Validation(
                    $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                    $"The {resourceName} identity is required."))
            : ValidateContextAndRun(accessContext, operation);

    private static Task<Result<IReadOnlyList<T>>> List<T>(
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<IReadOnlyList<T>>>> operation) =>
        ValidateContextAndRun(accessContext, operation);

    private static Task<Result<IReadOnlyList<T>>> List<T>(
        bool invalidIdentity,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<IReadOnlyList<T>>>> operation) =>
        invalidIdentity
            ? Task.FromResult(
                Result<IReadOnlyList<T>>.Failure(
                    Error.Validation(
                        $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                        $"The {resourceName} identity is required.")))
            : ValidateContextAndRun(accessContext, operation);

    private static Task<Result<T>> Delete<T>(
        bool invalidIdentity,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<T>>> operation) =>
        invalidIdentity
            ? Failure<T>(
                Error.Validation(
                    $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                    $"The {resourceName} identity is required."))
            : ValidateContextAndRun(accessContext, operation);

    private static Task<Result<T>> ValidateContextAndRun<T>(
        ResourceAccessContext accessContext,
        Func<Task<Result<T>>> operation)
    {
        var error = ValidateAccessContext(accessContext);

        return error is null
            ? operation()
            : Failure<T>(error);
    }

    private static Error? ValidateResource<TIdentity>(
        ResourceEnvelope<TIdentity>? resource,
        ResourceKind expectedKind,
        ResourceAccessContext accessContext,
        string resourceName,
        bool isCreate)
        where TIdentity : struct
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return contextError;

        if (resource is null)
        {
            return Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.resource-required",
                $"A {resourceName} resource is required.");
        }

        if (resource.Kind != expectedKind)
        {
            return Error.Validation(
                "hive.resource.kind-invalid",
                $"The {resourceName} resource kind is invalid.");
        }

        if (isCreate && resource.Version != ResourceVersion.Initial)
        {
            return Error.Validation(
                "hive.resource.version-invalid",
                $"A new {resourceName} must start at resource version 1.");
        }

        if (!isCreate && resource.Version.Value <= 0)
        {
            return Error.Validation(
                "hive.resource.version-invalid",
                $"The {resourceName} resource version is invalid.");
        }

        if (isCreate &&
            resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Error.Validation(
                "hive.resource.lifecycle-invalid",
                $"A new {resourceName} must start in Active lifecycle state.");
        }

        if (resource.Owner != accessContext.PrincipalId)
        {
            return new Error(
                "hive.resource.owner-forbidden",
                ErrorCategory.Forbidden,
                $"The current principal does not own the {resourceName}.");
        }

        if (!resource.Scope.Matches(accessContext))
        {
            return new Error(
                "hive.resource.scope-forbidden",
                ErrorCategory.Forbidden,
                $"The current access context is outside the {resourceName} scope.");
        }

        return null;
    }

    private static Error? ValidateAccessContext(
        ResourceAccessContext accessContext)
    {
        if (accessContext is null)
        {
            return Error.Validation(
                "hive.management.identity-required",
                "A resource access context is required.");
        }

        if (accessContext.DeploymentId is null ||
            accessContext.PrincipalId is null)
        {
            return Error.Validation(
                "hive.management.identity-required",
                "Management access requires a deployment and principal identity.");
        }

        return null;
    }

    private static Task<Result<T>> TransitionWorkItem<T>(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string resourceName,
        Func<Task<Result<T>>> operation)
    {
        var validation = ValidateTransitionArguments(
            workItemId,
            expectedVersion,
            accessContext,
            resourceName);

        return validation is null
            ? operation()
            : Failure<T>(validation);
    }

    private static Error? ValidateTransitionArguments(
        WorkItemId workItemId,
        ResourceVersion expectedVersion,
        ResourceAccessContext accessContext,
        string resourceName)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return contextError;

        if (workItemId == default)
        {
            return Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.identity-required",
                $"The {resourceName} identity is required.");
        }

        if (expectedVersion.Value <= 0)
        {
            return Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.version-invalid",
                $"A positive {resourceName} version is required.");
        }

        return null;
    }

    private static ResourceVersion ReadVersion(EventEnvelope envelope)
    {
        if (envelope.Payload.TryGetProperty("version", out var property) &&
            property.ValueKind == System.Text.Json.JsonValueKind.Number &&
            property.TryGetInt64(out var version) &&
            version > 0)
        {
            return new ResourceVersion(version);
        }

        throw new EventSerializationException(
            Error.Validation(
                "hive.management.work-item.activity-invalid",
                "A WorkItem activity event does not contain a valid resource version."));
    }

    private static WorkItemStatus? ReadStatus(EventEnvelope envelope)
    {
        if (!envelope.Payload.TryGetProperty("status", out var property))
            return null;

        if (property.ValueKind == System.Text.Json.JsonValueKind.String &&
            Enum.TryParse<WorkItemStatus>(
                property.GetString(),
                true,
                out var textStatus))
        {
            return textStatus;
        }

        if (property.ValueKind == System.Text.Json.JsonValueKind.Number &&
            property.TryGetInt32(out var numericStatus) &&
            Enum.IsDefined((WorkItemStatus)numericStatus))
        {
            return (WorkItemStatus)numericStatus;
        }

        return null;
    }

    private static string? ReadString(
        EventEnvelope envelope,
        string propertyName) =>
        envelope.Payload.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == System.Text.Json.JsonValueKind.String
            ? property.GetString()
            : null;

    private static string ActivityMessage(
        string eventType,
        string? reason) =>
        eventType switch
        {
            "work-item.created" => "WorkItem created.",
            "work-item.approval-requested" => "Approval requested.",
            "work-item.approved" => "WorkItem approved.",
            "work-item.rejected" => string.IsNullOrWhiteSpace(reason)
                ? "WorkItem rejected."
                : $"WorkItem rejected: {reason}",
            _ => eventType
        };

    private static Task<Result<T>> Failure<T>(
        string resourceName,
        string codeSuffix) =>
        Failure<T>(
            Error.Validation(
                $"hive.management.{resourceName.Replace(' ', '-')}.{codeSuffix}",
                $"A {resourceName} is required."));

    private static Task<Result<T>> Failure<T>(Error error) =>
        Task.FromResult(Result<T>.Failure(error));
}
