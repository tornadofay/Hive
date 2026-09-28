using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Hive.Core;
using Hive.Persistence;

namespace Hive.Management;

internal sealed record ExecutionTargetCapabilityOverridesResult(
    IReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>> Overrides,
    IReadOnlyList<ProviderDiscoveryRoutingFailure> DiscoveryFailures);

internal sealed record ProviderDiscoveryRoutingFailure(
    ExecutionTargetId TargetId,
    string TargetKey,
    Error Error);

internal sealed class HiveProviderManagementService : HiveManagementServiceBase

{

    private readonly IProviderResourceStore _providerResources;

    private readonly IProviderConnectionTester? _providerConnectionTester;
    private readonly IProviderCapabilityDiscovery? _providerCapabilityDiscovery;
    private readonly ISecretStore? _secrets;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<ProviderDiscoveryCacheKey, ProviderDiscoverySnapshot> _discoveryCache = new();
    private readonly ConcurrentDictionary<ProviderDiscoveryCacheKey, DiscoveryGate> _discoveryLocks = new();

    private const int MaxCachedDiscoveries = 128;
    private long _discoveryGeneration;

    internal HiveProviderManagementService(
        IProviderResourceStore providerResources,
        IProviderConnectionTester? providerConnectionTester,
        ISecretStore? secrets,
        IProviderCapabilityDiscovery? providerCapabilityDiscovery,
        IClock? clock = null)
    {
        _providerResources = providerResources ?? throw new ArgumentNullException(nameof(providerResources));
        _providerConnectionTester = providerConnectionTester;
        _providerCapabilityDiscovery = providerCapabilityDiscovery;
        _secrets = secrets;
        _clock = clock ?? SystemClock.Instance;
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

        if (account.Value!.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active ||
            provider.Value!.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Conflict(
                    "hive.management.provider-discovery-resource-inactive",
                    "Provider capability discovery requires an active provider and provider account."));
        }

        var discoveryGeneration = Volatile.Read(ref _discoveryGeneration);
        var cacheKey = new ProviderDiscoveryCacheKey(
            provider.Value!.Id,
            provider.Value.Resource.Version,
            account.Value!.Id,
            account.Value.Resource.Version,
            target.Value.Endpoint.AbsoluteUri,
            discoveryGeneration);

        if (!forceRefresh &&
            _discoveryCache.TryGetValue(cacheKey, out var cached))
        {
            return Result<ProviderDiscoverySnapshot>.Success(cached);
        }

        var gate = AcquireDiscoveryGate(cacheKey);
        var gateEntered = false;

        try
        {
            await gate.Semaphore
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            gateEntered = true;

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

                if (discovered.IsFailure)
                    return discovered;

                var validationError = ValidateDiscoverySnapshot(
                    discovered.Value!,
                    provider.Value,
                    account.Value,
                    target.Value);

                if (validationError is not null)
                {
                    return Result<ProviderDiscoverySnapshot>.Failure(validationError);
                }

                if (Volatile.Read(ref _discoveryGeneration) == discoveryGeneration)
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
            if (gateEntered)
                gate.Semaphore.Release();

            ReleaseDiscoveryGate(cacheKey, gate);
        }
    }

    private static Error? ValidateDiscoverySnapshot(
        ProviderDiscoverySnapshot snapshot,
        Provider provider,
        ProviderAccount account,
        ExecutionTarget target)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(target);

        if (snapshot.ProviderId != provider.Id ||
            snapshot.ProviderAccountId != account.Id ||
            !ProviderEndpointIdentity.Equals(
                snapshot.Endpoint,
                target.Endpoint))
        {
            return Error.Validation(
                "hive.management.provider-discovery-result-mismatch",
                "The provider discovery implementation returned metadata for a different provider, account, or execution target.");
        }

        return null;
    }

    private DiscoveryGate AcquireDiscoveryGate(
        ProviderDiscoveryCacheKey key)
    {
        while (true)
        {
            var gate = _discoveryLocks.GetOrAdd(
                key,
                static _ => new DiscoveryGate());

            lock (gate.SyncRoot)
            {
                if (gate.Retired)
                    continue;

                gate.ReferenceCount++;
                return gate;
            }
        }
    }

    private void ReleaseDiscoveryGate(
        ProviderDiscoveryCacheKey key,
        DiscoveryGate gate)
    {
        lock (gate.SyncRoot)
        {
            gate.ReferenceCount--;

            if (gate.ReferenceCount != 0)
                return;

            if (_discoveryLocks.TryGetValue(key, out var currentGate) &&
                ReferenceEquals(currentGate, gate) &&
                _discoveryLocks.TryRemove(key, out _))
            {
                gate.Retired = true;
                gate.Semaphore.Dispose();
            }
        }
    }

    internal void InvalidateProviderDiscoveryCache()
    {
        Interlocked.Increment(ref _discoveryGeneration);
        _discoveryCache.Clear();
    }

    private void TrimDiscoveryCache(
        ProviderDiscoveryCacheKey preferredKey)
    {
        while (_discoveryCache.Count > MaxCachedDiscoveries)
        {
            var currentGeneration = Volatile.Read(ref _discoveryGeneration);
            foreach (var key in _discoveryCache.Keys)
            {
                if (key.DiscoveryGeneration != currentGeneration &&
                    _discoveryCache.TryRemove(key, out _))
                {
                    if (_discoveryCache.Count <= MaxCachedDiscoveries)
                        return;
                }
            }

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

    internal async Task<Result<ExecutionTargetCapabilityOverridesResult>>
        GetExecutionTargetCapabilityOverridesAsync(
            IReadOnlyList<ExecutionTarget> targets,
            CapabilityKey requiredCapability,
            ResourceAccessContext accessContext,
            CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targets);

        if (string.IsNullOrWhiteSpace(requiredCapability.Value))
        {
            return Result<ExecutionTargetCapabilityOverridesResult>.Failure(
                Error.Validation(
                    "hive.management.execution-target-capability-required",
                    "A required execution target capability is required."));
        }

        if (targets.Any(static target => target is null))
        {
            return Result<ExecutionTargetCapabilityOverridesResult>.Failure(
                Error.Validation(
                    "hive.management.execution-targets-invalid",
                    "Execution target routing input cannot contain null targets."));
        }

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
        {
            return Result<ExecutionTargetCapabilityOverridesResult>.Failure(
                contextError);
        }

        if (_providerCapabilityDiscovery is null || targets.Count == 0)
        {
            return Result<ExecutionTargetCapabilityOverridesResult>.Success(
                new ExecutionTargetCapabilityOverridesResult(
                    new ReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>(
                        new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>()),
                    Array.Empty<ProviderDiscoveryRoutingFailure>()));
        }

        var overrides =
            new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>();
        var discoveryFailures = new List<ProviderDiscoveryRoutingFailure>();

        foreach (var target in targets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (target.Capabilities.Any(
                    capability => capability.Capability == requiredCapability))
            {
                continue;
            }

            var discovery = await GetProviderDiscoveryAsync(
                target.Id,
                accessContext,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (discovery.IsFailure)
            {
                discoveryFailures.Add(
                    new ProviderDiscoveryRoutingFailure(
                        target.Id,
                        target.Key,
                        discovery.Error!));
                continue;
            }

            var snapshot = discovery.Value!;

            if (snapshot.IsStale(_clock.UtcNow))
            {
                discovery = await GetProviderDiscoveryAsync(
                    target.Id,
                    accessContext,
                    forceRefresh: true,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (discovery.IsFailure)
                {
                    discoveryFailures.Add(
                        new ProviderDiscoveryRoutingFailure(
                            target.Id,
                            target.Key,
                            discovery.Error!));
                    continue;
                }

                snapshot = discovery.Value!;

                if (snapshot.IsStale(_clock.UtcNow))
                    continue;
            }

            var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
                target,
                snapshot,
                _clock.UtcNow);

            if (effective.Count != target.Capabilities.Count)
                overrides[target.Id] = effective;
        }

        return Result<ExecutionTargetCapabilityOverridesResult>.Success(
            new ExecutionTargetCapabilityOverridesResult(
                new ReadOnlyDictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>(overrides),
                discoveryFailures));
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
        ResourceAccessContext accessContext,
        bool includeRetired = false,
        CancellationToken cancellationToken = default) =>
        List(
            false,
            accessContext,
            "execution target",
            () => _providerResources.ListExecutionTargetsAsync(
                accessContext,
                includeRetired,
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




    private sealed class DiscoveryGate
    {
        public readonly object SyncRoot = new();

        public readonly SemaphoreSlim Semaphore = new(1, 1);

        public int ReferenceCount;

        public bool Retired;
    }

    private readonly record struct ProviderDiscoveryCacheKey(
        ProviderId ProviderId,
        ResourceVersion ProviderVersion,
        ProviderAccountId ProviderAccountId,
        ResourceVersion ProviderAccountVersion,
        string Endpoint,
        long DiscoveryGeneration);
}