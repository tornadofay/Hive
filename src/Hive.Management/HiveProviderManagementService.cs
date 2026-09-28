using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Hive.Core;
using Hive.Persistence;



namespace Hive.Management;



internal sealed class HiveProviderManagementService : HiveManagementServiceBase

{

    private readonly IProviderResourceStore _providerResources;

    private readonly IProviderConnectionTester? _providerConnectionTester;
    private readonly IProviderCapabilityDiscovery? _providerCapabilityDiscovery;
    private readonly ISecretStore? _secrets;
    private readonly ConcurrentDictionary<ProviderDiscoveryCacheKey, ProviderDiscoverySnapshot> _discoveryCache = new();
    private readonly ConcurrentDictionary<ProviderDiscoveryCacheKey, SemaphoreSlim> _discoveryLocks = new();

    private const int MaxCachedDiscoveries = 128;

    internal HiveProviderManagementService(
        IProviderResourceStore providerResources,
        IProviderConnectionTester? providerConnectionTester,
        ISecretStore? secrets,
        IProviderCapabilityDiscovery? providerCapabilityDiscovery)
    {
        _providerResources = providerResources ?? throw new ArgumentNullException(nameof(providerResources));
        _providerConnectionTester = providerConnectionTester;
        _providerCapabilityDiscovery = providerCapabilityDiscovery;
        _secrets = secrets;
    }



    internal async Task<Result<ProviderDiscoverySnapshot>> GetProviderDiscoveryAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<ProviderDiscoverySnapshot>.Failure(contextError);

        if (executionTargetId == default)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Validation(
                    "hive.management.execution-target.identity-required",
                    "The execution target identity is required."));
        }

        if (_providerCapabilityDiscovery is null)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Unsupported(
                    "hive.management.provider-discovery-unavailable",
                    "Provider capability discovery is not configured."));
        }

        var target = await _providerResources.GetExecutionTargetAsync(
            executionTargetId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (target.IsFailure)
            return Result<ProviderDiscoverySnapshot>.Failure(target.Error!);

        if (target.Value!.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Conflict(
                    "hive.management.execution-target-inactive",
                    "Provider discovery requires an active execution target."));
        }

        var account = await _providerResources.GetProviderAccountAsync(
            target.Value.ProviderAccountId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (account.IsFailure)
            return Result<ProviderDiscoverySnapshot>.Failure(account.Error!);

        var provider = await _providerResources.GetProviderAsync(
            target.Value.ProviderId,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (provider.IsFailure)
            return Result<ProviderDiscoverySnapshot>.Failure(provider.Error!);

        var cacheKey = new ProviderDiscoveryCacheKey(
            provider.Value!.Id,
            provider.Value.Resource.Version,
            account.Value!.Id,
            account.Value.Resource.Version,
            target.Value.Id,
            target.Value.Resource.Version,
            target.Value.Endpoint.AbsoluteUri);

        if (!forceRefresh &&
            _discoveryCache.TryGetValue(cacheKey, out var cached))
        {
            return Result<ProviderDiscoverySnapshot>.Success(cached);
        }

        var gate = _discoveryLocks.GetOrAdd(
            cacheKey,
            static _ => new SemaphoreSlim(1, 1));

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (!forceRefresh &&
                _discoveryCache.TryGetValue(cacheKey, out cached))
            {
                return Result<ProviderDiscoverySnapshot>.Success(cached);
            }

            SecretMaterial? material = null;

            try
            {
                if (account.Value.CredentialSecret is not null)
                {
                    if (_secrets is null)
                    {
                        return Result<ProviderDiscoverySnapshot>.Failure(
                            Error.Unsupported(
                                "hive.management.secret-store-unavailable",
                                "The provider account references a credential but the Secret Store is not configured."));
                    }

                    var secret = await _secrets.GetAsync(
                        account.Value.CredentialSecret.Value.Id,
                        accessContext,
                        cancellationToken).ConfigureAwait(false);

                    if (secret.IsFailure)
                        return Result<ProviderDiscoverySnapshot>.Failure(secret.Error!);

                    material = secret.Value!.Material;
                }

                var discovered = await _providerCapabilityDiscovery
                    .DiscoverAsync(
                        provider.Value,
                        account.Value,
                        target.Value,
                        material,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (discovered.IsSuccess)
                {
                    _discoveryCache[cacheKey] = discovered.Value!;
                    TrimDiscoveryCache(cacheKey);
                }

                return discovered;
            }
            finally
            {
                material?.Dispose();
            }
        }
        finally
        {
            gate.Release();
        }
    }

    private void TrimDiscoveryCache(
        ProviderDiscoveryCacheKey preferredKey)
    {
        while (_discoveryCache.Count > MaxCachedDiscoveries)
        {
            var removed = false;

            foreach (var key in _discoveryCache.Keys)
            {
                if (key.Equals(preferredKey))
                    continue;

                if (_discoveryCache.TryRemove(key, out _))
                {
                    removed = true;
                    break;
                }
            }

            if (!removed)
                return;
        }
    }

    internal async Task<Result<IReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>>>
        GetExecutionTargetCapabilityOverridesAsync(
            IReadOnlyList<ExecutionTarget> targets,
            ResourceAccessContext accessContext,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (targets.Any(static target => target is null))
        {
            return Result<IReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>>.Failure(
                Error.Validation(
                    "hive.management.execution-targets-invalid",
                    "Execution target routing input cannot contain null targets."));
        }

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
        {
            return Result<IReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>>.Failure(
                contextError);
        }

        if (_providerCapabilityDiscovery is null || targets.Count == 0)
        {
            return Result<IReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>>.Success(
                new ReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>(
                    new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>()));
        }

        var overrides =
            new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>();

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var discovery = await GetProviderDiscoveryAsync(
                target.Id,
                accessContext,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (discovery.IsFailure)
                continue;

            var snapshot = discovery.Value!;

            if (snapshot.IsStale(DateTimeOffset.UtcNow))
            {
                discovery = await GetProviderDiscoveryAsync(
                    target.Id,
                    accessContext,
                    forceRefresh: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (discovery.IsFailure)
                    continue;

                snapshot = discovery.Value!;

                if (snapshot.IsStale(DateTimeOffset.UtcNow))
                    continue;
            }

            var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
                target,
                snapshot,
                DateTimeOffset.UtcNow);

            if (effective.Count != target.Capabilities.Count)
                overrides[target.Id] = effective;
        }

        return Result<IReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>>.Success(
            new ReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>(overrides));
    }

    internal async Task<Result<ProviderConnectionTestResult>> TestExecutionTargetConnectionAsync(
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


    internal Task<Result<Provider>> CreateProviderAsync(
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


    internal Task<Result<Provider>> GetProviderAsync(
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


    internal Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(
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


    internal Task<Result<Provider>> UpdateProviderAsync(
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


    internal Task<Result<Provider>> DeleteProviderAsync(
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


    internal Task<Result<Provider>> ReactivateProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            providerId == default,
            accessContext,
            "provider",
            () => _providerResources.ReactivateProviderAsync(
                providerId,
                accessContext,
                cancellationToken));


    internal Task<Result<ProviderAccount>> CreateProviderAccountAsync(
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


    internal Task<Result<ProviderAccount>> GetProviderAccountAsync(
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


    internal Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(
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


    internal Task<Result<ProviderAccount>> UpdateProviderAccountAsync(
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


    internal Task<Result<ProviderAccount>> DeleteProviderAccountAsync(
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


    internal Task<Result<ProviderAccount>> ReactivateProviderAccountAsync(
        ProviderAccountId providerAccountId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            providerAccountId == default,
            accessContext,
            "provider account",
            () => _providerResources.ReactivateProviderAccountAsync(
                providerAccountId,
                accessContext,
                cancellationToken));


    internal Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(
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


    internal Task<Result<ExecutionTarget>> GetExecutionTargetAsync(
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


    internal Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(
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


    internal Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(
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


    internal Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(
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


    internal Task<Result<ExecutionTarget>> ReactivateExecutionTargetAsync(
        ExecutionTargetId executionTargetId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default) =>
        Delete(
            executionTargetId == default,
            accessContext,
            "execution target",
            () => _providerResources.ReactivateExecutionTargetAsync(
                executionTargetId,
                accessContext,
                cancellationToken));




    private readonly record struct ProviderDiscoveryCacheKey(
        ProviderId ProviderId,
        ResourceVersion ProviderVersion,
        ProviderAccountId ProviderAccountId,
        ResourceVersion ProviderAccountVersion,
        ExecutionTargetId ExecutionTargetId,
        ResourceVersion ExecutionTargetVersion,
        string Endpoint);
}