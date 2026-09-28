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
    private readonly HiveSecretManagementService _secretManagement;
    private readonly IClock _clock;
    private readonly ConcurrentDictionary<ProviderDiscoveryCacheKey, ProviderDiscoveryCacheEntry> _discoveryCache = new();
    private long _discoveryCompletionSequence;
    private readonly ConcurrentDictionary<ProviderDiscoveryCacheKey, DiscoveryGate> _discoveryLocks = new();
    private readonly ConcurrentDictionary<ProviderId, SemaphoreSlim> _providerRefreshLocks = new();

    private const int MaxCachedDiscoveries = 128;
    private long _discoveryGeneration;

    internal HiveProviderManagementService(
        IProviderResourceStore providerResources,
        IProviderConnectionTester? providerConnectionTester,
        ISecretStore? secrets,
        IProviderCapabilityDiscovery? providerCapabilityDiscovery,
        HiveSecretManagementService secretManagement,
        IClock? clock = null)
    {
        _providerResources = providerResources ?? throw new ArgumentNullException(nameof(providerResources));
        _providerConnectionTester = providerConnectionTester;
        _providerCapabilityDiscovery = providerCapabilityDiscovery;
        _secrets = secrets;
        _secretManagement = secretManagement ?? throw new ArgumentNullException(nameof(secretManagement));
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

        var cachedCompletionSequenceAtRequest =
            _discoveryCache.TryGetValue(
                cacheKey,
                out var cachedAtRequest)
                ? cachedAtRequest.CompletionSequence
                : 0;

        if (!forceRefresh &&
            cachedAtRequest is not null)
        {
            return Result<ProviderDiscoverySnapshot>.Success(
                cachedAtRequest.Snapshot);
        }

        var gate = AcquireDiscoveryGate(cacheKey);
        var gateEntered = false;

        try
        {
            await gate.Semaphore
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);

            gateEntered = true;

            if (forceRefresh &&
                _discoveryCache.TryGetValue(
                    cacheKey,
                    out var cachedAfterWait) &&
                cachedAfterWait.WasForcedRefresh &&
                cachedAfterWait.CompletionSequence > cachedCompletionSequenceAtRequest)
            {
                return Result<ProviderDiscoverySnapshot>.Success(
                    cachedAfterWait.Snapshot);
            }

            if (!forceRefresh &&
                _discoveryCache.TryGetValue(cacheKey, out var cached))
            {
                return Result<ProviderDiscoverySnapshot>.Success(
                    cached.Snapshot);
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
                    var completionSequence =
                        Interlocked.Increment(ref _discoveryCompletionSequence);

                    _discoveryCache[cacheKey] =
                        new ProviderDiscoveryCacheEntry(
                            discovered.Value!,
                            completionSequence,
                            forceRefresh);

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


    internal async Task<Result<ProviderSettingsOperationResult>> ConfigureBuiltInProviderAsync(
        string providerKey,
        SecretMaterial? credential,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<ProviderSettingsOperationResult>.Failure(contextError);

        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Validation(
                    "hive.management.provider-catalog-key-required",
                    "A built-in provider catalog key is required."));
        }

        var catalog = BuiltInProviderCatalog.Find(providerKey);
        if (catalog is null)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                new Error(
                    "hive.management.provider-catalog-provider-not-found",
                    ErrorCategory.NotFound,
                    "The selected provider is not available in the built-in provider catalog."));
        }

        if (!catalog.NormalOnboardingSupported)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Unsupported(
                    "hive.management.provider-catalog-advanced-required",
                    catalog.OnboardingNote ?? "This provider requires Advanced Configuration."));
        }

        if (catalog.RequiresCredential && credential is null)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Validation(
                    "hive.management.provider-credential-required",
                    "The selected provider requires credential material."));
        }

        var providers = await _providerResources
            .ListProvidersAsync(
                accessContext,
                includeRetired: true,
                cancellationToken)
            .ConfigureAwait(false);

        if (providers.IsFailure)
            return Result<ProviderSettingsOperationResult>.Failure(providers.Error!);

        var provider = providers.Value!.FirstOrDefault(
            item => string.Equals(item.Key, catalog.Key, StringComparison.OrdinalIgnoreCase));

        if (provider is not null &&
            provider.Resource.Lifecycle.Status == ResourceLifecycleStatus.Active)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Conflict(
                    "hive.management.provider-already-configured",
                    $"Provider '{catalog.DisplayName}' is already configured."));
        }

        if (provider is not null)
        {
            var reactivated = await ReactivateProviderAsync(
                provider.Id,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (reactivated.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(reactivated.Error!);

            provider = reactivated.Value!;
        }
        else
        {
            var now = _clock.UtcNow;
            provider = new Provider(
                new ResourceEnvelope<ProviderId>(
                    ResourceKind.Provider,
                    ProviderId.New(),
                    accessContext.PrincipalId!.Value,
                    accessContext.TenantId is { } tenantId
                        ? ResourceScope.Tenant(tenantId)
                        : ResourceScope.Global(),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        accessContext.PrincipalId.Value,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                catalog.Key,
                catalog.DisplayName,
                catalog.TransportKind);

            var created = await CreateProviderAsync(
                provider,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (created.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(created.Error!);

            provider = created.Value!;
        }

        var accounts = await ListProviderAccountsAsync(
            provider.Id,
            accessContext,
            includeRetired: true,
            cancellationToken).ConfigureAwait(false);

        if (accounts.IsFailure)
            return Result<ProviderSettingsOperationResult>.Failure(accounts.Error!);

        var account = accounts.Value!.FirstOrDefault(
            item => string.Equals(item.Key, "default", StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            var now = _clock.UtcNow;
            account = new ProviderAccount(
                new ResourceEnvelope<ProviderAccountId>(
                    ResourceKind.ProviderAccount,
                    ProviderAccountId.New(),
                    accessContext.PrincipalId!.Value,
                    accessContext.TenantId is { } tenantId
                        ? ResourceScope.Tenant(tenantId)
                        : ResourceScope.Global(),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        accessContext.PrincipalId.Value,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                provider.Id,
                "default",
                "Default Account");

            var created = await CreateProviderAccountAsync(
                account,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (created.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(created.Error!);

            account = created.Value!;
        }
        else if (account.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
        {
            var reactivated = await ReactivateProviderAccountAsync(
                account.Id,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (reactivated.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(reactivated.Error!);

            account = reactivated.Value!;
        }

        if (catalog.RequiresCredential)
        {
            var credentialResult = await SetAccountCredentialAsync(
                provider,
                account,
                credential!,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (credentialResult.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(credentialResult.Error!);

            account = credentialResult.Value!;
        }

        InvalidateProviderDiscoveryCache();

        var refreshed = await RefreshProviderCoreAsync(
            provider,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        return refreshed.IsFailure
            ? Result<ProviderSettingsOperationResult>.Failure(refreshed.Error!)
            : refreshed;
    }


    internal async Task<Result<ProviderSettingsOperationResult>> ReplaceBuiltInProviderCredentialAsync(
        ProviderId providerId,
        SecretMaterial credential,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credential);

        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<ProviderSettingsOperationResult>.Failure(contextError);

        if (providerId == default)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Validation(
                    "hive.management.provider.identity-required",
                    "The provider identity is required."));
        }

        var providerResult = await _providerResources
            .GetProviderAsync(providerId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (providerResult.IsFailure)
            return Result<ProviderSettingsOperationResult>.Failure(providerResult.Error!);

        var provider = providerResult.Value!;
        var catalog = BuiltInProviderCatalog.Find(provider.Key);

        if (catalog is null || !catalog.NormalOnboardingSupported)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Unsupported(
                    "hive.management.provider-advanced-credential-replacement-required",
                    "This provider's credential is not managed by the normal Provider Settings surface."));
        }

        if (provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Conflict(
                    "hive.management.provider-inactive",
                    "Provider credential replacement requires an active provider."));
        }

        var accounts = await ListProviderAccountsAsync(
            provider.Id,
            accessContext,
            includeRetired: true,
            cancellationToken).ConfigureAwait(false);

        if (accounts.IsFailure)
            return Result<ProviderSettingsOperationResult>.Failure(accounts.Error!);

        var account = accounts.Value!.FirstOrDefault(
            item => string.Equals(item.Key, "default", StringComparison.OrdinalIgnoreCase));

        if (account is null)
        {
            var now = _clock.UtcNow;
            account = new ProviderAccount(
                new ResourceEnvelope<ProviderAccountId>(
                    ResourceKind.ProviderAccount,
                    ProviderAccountId.New(),
                    accessContext.PrincipalId!.Value,
                    accessContext.TenantId is { } tenantId
                        ? ResourceScope.Tenant(tenantId)
                        : ResourceScope.Global(),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        accessContext.PrincipalId.Value,
                        now,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(now)),
                provider.Id,
                "default",
                "Default Account");

            var created = await CreateProviderAccountAsync(
                account,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (created.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(created.Error!);

            account = created.Value!;
        }
        else if (account.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
        {
            var reactivated = await ReactivateProviderAccountAsync(
                account.Id,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (reactivated.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(reactivated.Error!);

            account = reactivated.Value!;
        }

        var credentialResult = await SetAccountCredentialAsync(
            provider,
            account,
            credential,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (credentialResult.IsFailure)
            return Result<ProviderSettingsOperationResult>.Failure(credentialResult.Error!);

        InvalidateProviderDiscoveryCache();

        var refreshed = await RefreshProviderCoreAsync(
            provider,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        return refreshed.IsFailure
            ? Result<ProviderSettingsOperationResult>.Failure(refreshed.Error!)
            : refreshed;
    }


    internal async Task<Result<ProviderSettingsOperationResult>> RefreshProviderAsync(
        ProviderId providerId,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken = default)
    {
        var contextError = ValidateAccessContext(accessContext);
        if (contextError is not null)
            return Result<ProviderSettingsOperationResult>.Failure(contextError);

        if (providerId == default)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Validation(
                    "hive.management.provider.identity-required",
                    "The provider identity is required."));
        }

        var provider = await _providerResources
            .GetProviderAsync(providerId, accessContext, cancellationToken)
            .ConfigureAwait(false);

        if (provider.IsFailure)
            return Result<ProviderSettingsOperationResult>.Failure(provider.Error!);

        return await RefreshProviderCoreAsync(
            provider.Value!,
            accessContext,
            cancellationToken).ConfigureAwait(false);
    }


    private async Task<Result<ProviderSettingsOperationResult>> RefreshProviderCoreAsync(
        Provider provider,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (provider.Resource.Lifecycle.Status != ResourceLifecycleStatus.Active)
        {
            return Result<ProviderSettingsOperationResult>.Failure(
                Error.Conflict(
                    "hive.management.provider-inactive",
                    "Provider discovery refresh requires an active provider."));
        }

        var refreshGate = _providerRefreshLocks.GetOrAdd(
            provider.Id,
            static _ => new SemaphoreSlim(1, 1));

        await refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var catalog = BuiltInProviderCatalog.Find(provider.Key);
            var accounts = await ListProviderAccountsAsync(
                provider.Id,
                accessContext,
                includeRetired: false,
                cancellationToken).ConfigureAwait(false);

            if (accounts.IsFailure)
                return Result<ProviderSettingsOperationResult>.Failure(accounts.Error!);

            var errors = new List<Error>();
            var attempted = 0;
            var succeeded = 0;
            var models = 0;
            var created = 0;
            var reactivated = 0;
            var retired = 0;

            foreach (var account in accounts.Value!)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var targets = await ListExecutionTargetsAsync(
                    account.Id,
                    accessContext,
                    includeRetired: true,
                    cancellationToken).ConfigureAwait(false);

                if (targets.IsFailure)
                {
                    errors.Add(targets.Error!);
                    continue;
                }

                var endpoints = targets.Value!
                    .Select(target => target.Endpoint)
                    .Concat(
                        catalog?.DefaultEndpoint is { } defaultEndpoint
                            ? [defaultEndpoint]
                            : Array.Empty<Uri>())
                    .Distinct()
                    .ToArray();

                foreach (var endpoint in endpoints)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    attempted++;

                    var discovered = await DiscoverEndpointAsync(
                        provider,
                        account,
                        endpoint,
                        accessContext,
                        cancellationToken).ConfigureAwait(false);

                    if (discovered.IsFailure)
                    {
                        errors.Add(discovered.Error!);
                        continue;
                    }

                    succeeded++;
                    var snapshot = discovered.Value!;

                    if (snapshot.ModelEnumerationState != ProviderDiscoveryState.Supported ||
                        snapshot.IsStale(_clock.UtcNow))
                    {
                        continue;
                    }

                    models += snapshot.Models.Count;

                    var reconciled = await ReconcileAutomaticTargetsAsync(
                        provider,
                        account,
                        snapshot,
                        accessContext,
                        cancellationToken).ConfigureAwait(false);

                    if (reconciled.IsFailure)
                    {
                        errors.Add(reconciled.Error!);
                        continue;
                    }

                    created += reconciled.Value!.Created;
                    reactivated += reconciled.Value.Reactivated;
                    retired += reconciled.Value.Retired;
                }
            }

            var activeAutomaticTargets = 0;
            foreach (var account in accounts.Value!)
            {
                var targets = await ListExecutionTargetsAsync(
                    account.Id,
                    accessContext,
                    includeRetired: false,
                    cancellationToken).ConfigureAwait(false);

                if (targets.IsFailure)
                {
                    errors.Add(targets.Error!);
                    continue;
                }

                activeAutomaticTargets += targets.Value!
                    .Count(target =>
                    {
                        try
                        {
                            return target.ManagementMode == ExecutionTargetManagementMode.Automatic;
                        }
                        catch (InvalidOperationException)
                        {
                            return false;
                        }
                    });
            }

            return Result<ProviderSettingsOperationResult>.Success(
                new ProviderSettingsOperationResult(
                    provider,
                    attempted,
                    succeeded,
                    models,
                    created,
                    reactivated,
                    retired,
                    errors)
                {
                    ActiveAutomaticTargetCount = activeAutomaticTargets
                });
        }
        finally
        {
            refreshGate.Release();
        }
    }


    private async Task<Result<ProviderAccount>> SetAccountCredentialAsync(
        Provider provider,
        ProviderAccount account,
        SecretMaterial credential,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (_secrets is null)
        {
            return Result<ProviderAccount>.Failure(
                Error.Unsupported(
                    "hive.management.secret-store-unavailable",
                    "The Hive Secret Store is not configured."));
        }

        if (account.CredentialSecret is { } existingReference)
        {
            var descriptor = await _secretManagement.GetSecretDescriptorAsync(
                existingReference.Id,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (descriptor.IsFailure)
                return Result<ProviderAccount>.Failure(descriptor.Error!);

            var replaced = await _secretManagement.ReplaceSecretAsync(
                existingReference.Id,
                credential,
                descriptor.Value!.Resource.Version,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (replaced.IsFailure)
                return Result<ProviderAccount>.Failure(replaced.Error!);

            return Result<ProviderAccount>.Success(account);
        }

        var key = $"provider-{provider.Key}-api-key";
        var secretResult = await _secretManagement.CreateSecretAsync(
            key,
            $"{provider.DisplayName} API Key",
            credential,
            accessContext,
            cancellationToken).ConfigureAwait(false);

        if (secretResult.IsFailure)
            return Result<ProviderAccount>.Failure(secretResult.Error!);

        var updated = account.WithCredentialSecret(
            new SecretReference(secretResult.Value!.Id));

        return await UpdateProviderAccountAsync(
            updated,
            accessContext,
            cancellationToken).ConfigureAwait(false);
    }


    private async Task<Result<ProviderDiscoverySnapshot>> DiscoverEndpointAsync(
        Provider provider,
        ProviderAccount account,
        Uri endpoint,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        if (_providerCapabilityDiscovery is null)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Unsupported(
                    "hive.management.provider-discovery-unavailable",
                    "Provider capability discovery is not configured."));
        }

        SecretMaterial? material = null;

        try
        {
            if (account.CredentialSecret is { } reference)
            {
                if (_secrets is null)
                {
                    return Result<ProviderDiscoverySnapshot>.Failure(
                        Error.Unsupported(
                            "hive.management.secret-store-unavailable",
                            "The provider account references a credential but the Secret Store is not configured."));
                }

                var secret = await _secrets.GetAsync(
                    reference.Id,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (secret.IsFailure)
                    return Result<ProviderDiscoverySnapshot>.Failure(secret.Error!);

                material = secret.Value!.Material;
            }

            var probe = CreateDiscoveryProbeTarget(
                provider,
                account,
                endpoint,
                accessContext);

            var discovered = await _providerCapabilityDiscovery
                .DiscoverAsync(
                    provider,
                    account,
                    probe,
                    material,
                    cancellationToken)
                .ConfigureAwait(false);

            if (discovered.IsFailure)
                return discovered;

            var validationError = ValidateDiscoverySnapshot(
                discovered.Value!,
                provider,
                account,
                probe);

            if (validationError is not null)
                return Result<ProviderDiscoverySnapshot>.Failure(validationError);

            if (discovered.Value!.IsStale(_clock.UtcNow))
            {
                return Result<ProviderDiscoverySnapshot>.Failure(
                    Error.Conflict(
                        "hive.management.provider-discovery-stale",
                        "Fresh provider discovery returned stale operational metadata."));
            }

            return discovered;
        }
        finally
        {
            material?.Dispose();
        }
    }


    private ExecutionTarget CreateDiscoveryProbeTarget(
        Provider provider,
        ProviderAccount account,
        Uri endpoint,
        ResourceAccessContext accessContext)
    {
        var now = _clock.UtcNow;
        var scope = accessContext.TenantId is { } tenantId
            ? ResourceScope.Tenant(tenantId)
            : ResourceScope.Global();

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                accessContext.PrincipalId!.Value,
                scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    accessContext.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            account.Id,
            "discovery-probe",
            "Provider Discovery Probe",
            endpoint,
            "discovery-probe",
            null,
            Array.Empty<CapabilityStateEntry>());
    }


    private async Task<Result<ReconciliationCounts>> ReconcileAutomaticTargetsAsync(
        Provider provider,
        ProviderAccount account,
        ProviderDiscoverySnapshot snapshot,
        ResourceAccessContext accessContext,
        CancellationToken cancellationToken)
    {
        var targetsResult = await ListExecutionTargetsAsync(
            account.Id,
            accessContext,
            includeRetired: true,
            cancellationToken).ConfigureAwait(false);

        if (targetsResult.IsFailure)
            return Result<ReconciliationCounts>.Failure(targetsResult.Error!);

        var allTargets = targetsResult.Value!;
        var automaticTargets = allTargets
            .Where(IsAutomaticTarget)
            .Where(target => target.ProviderId == provider.Id)
            .Where(target => target.Endpoint.Equals(snapshot.Endpoint))
            .ToList();

        var discoveredModelIds = snapshot.Models
            .Select(model => model.ModelId)
            .ToHashSet(StringComparer.Ordinal);

        var created = 0;
        var reactivated = 0;
        var retired = 0;

        foreach (var model in snapshot.Models)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var matching = automaticTargets
                .Where(target =>
                    string.Equals(target.Model, model.ModelId, StringComparison.Ordinal) &&
                    target.Deployment is null)
                .OrderBy(target => target.Id.Value)
                .FirstOrDefault();

            if (matching is null)
            {
                var createdTarget = CreateAutomaticTarget(
                    provider,
                    account,
                    snapshot.Endpoint,
                    model,
                    accessContext,
                    _clock.UtcNow);

                var createResult = await CreateExecutionTargetAsync(
                    createdTarget,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (createResult.IsFailure)
                    return Result<ReconciliationCounts>.Failure(createResult.Error!);

                created++;
                continue;
            }

            var current = matching;

            if (current.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
            {
                var activated = await ReactivateExecutionTargetAsync(
                    current.Id,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (activated.IsFailure)
                    return Result<ReconciliationCounts>.Failure(activated.Error!);

                current = activated.Value!;
                reactivated++;
            }

            var desired = current
                .WithDisplayName(model.ModelId)
                .WithEndpoint(snapshot.Endpoint)
                .WithModel(model.ModelId)
                .WithDeployment(null)
                .WithCapabilities(model.DiscoveredCapabilities)
                .WithManagementMode(ExecutionTargetManagementMode.Automatic);

            if (!ExecutionTargetEquivalent(current, desired))
            {
                var updated = await UpdateExecutionTargetAsync(
                    desired,
                    accessContext,
                    cancellationToken).ConfigureAwait(false);

                if (updated.IsFailure)
                    return Result<ReconciliationCounts>.Failure(updated.Error!);
            }
        }

        foreach (var target in automaticTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modelId = target.Model ?? target.Deployment;
            if (modelId is null || discoveredModelIds.Contains(modelId))
                continue;

            if (target.Resource.Lifecycle.Status == ResourceLifecycleStatus.Retired)
                continue;

            var retiredResult = await DeleteExecutionTargetAsync(
                target.Id,
                accessContext,
                cancellationToken).ConfigureAwait(false);

            if (retiredResult.IsFailure)
                return Result<ReconciliationCounts>.Failure(retiredResult.Error!);

            retired++;
        }

        return Result<ReconciliationCounts>.Success(
            new ReconciliationCounts(created, reactivated, retired));
    }


    private static ExecutionTarget CreateAutomaticTarget(
        Provider provider,
        ProviderAccount account,
        Uri endpoint,
        ProviderModelMetadata model,
        ResourceAccessContext accessContext,
        DateTimeOffset now)
    {
        var identity = $"{account.Id.Value:N}|{endpoint.AbsoluteUri}|model:{model.ModelId}";
        var hash = Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(identity)))
            .ToLowerInvariant();

        now = now.ToUniversalTime();
        var scope = accessContext.TenantId is { } tenantId
            ? ResourceScope.Tenant(tenantId)
            : ResourceScope.Global();

        var target = new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                accessContext.PrincipalId!.Value,
                scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    accessContext.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            account.Id,
            $"auto-{hash[..32]}",
            model.ModelId,
            endpoint,
            model.ModelId,
            null,
            model.DiscoveredCapabilities);

        return target.WithManagementMode(ExecutionTargetManagementMode.Automatic);
    }


    private static bool IsAutomaticTarget(ExecutionTarget target)
    {
        try
        {
            return target.ManagementMode == ExecutionTargetManagementMode.Automatic;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }


    private static bool ExecutionTargetEquivalent(
        ExecutionTarget left,
        ExecutionTarget right) =>
        string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) &&
        left.Endpoint.Equals(right.Endpoint) &&
        string.Equals(left.Model, right.Model, StringComparison.Ordinal) &&
        string.Equals(left.Deployment, right.Deployment, StringComparison.Ordinal) &&
        left.Capabilities.SequenceEqual(right.Capabilities);
    

    private readonly record struct ReconciliationCounts(
        int Created,
        int Reactivated,
        int Retired);


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




    private sealed record ProviderDiscoveryCacheEntry(
        ProviderDiscoverySnapshot Snapshot,
        long CompletionSequence,
        bool WasForcedRefresh);

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