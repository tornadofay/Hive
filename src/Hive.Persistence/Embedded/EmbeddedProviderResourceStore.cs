using Hive.Core;

namespace Hive.Persistence;

public sealed class EmbeddedProviderResourceStore : IProviderResourceStore
{
    private readonly EmbeddedProviderStore _providers;
    private readonly EmbeddedProviderAccountStore _accounts;
    private readonly EmbeddedExecutionTargetStore _executionTargets;
    private readonly EmbeddedProviderResourceReader _reader;

    public EmbeddedProviderResourceStore(
        EmbeddedPersistenceDatabase options,
        IClock? clock = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        var effectiveClock = clock ?? SystemClock.Instance;
        _reader = new EmbeddedProviderResourceReader(options, effectiveClock);
        _providers = new EmbeddedProviderStore(options, _reader, effectiveClock);
        _accounts = new EmbeddedProviderAccountStore(options, _reader, effectiveClock);
        _executionTargets = new EmbeddedExecutionTargetStore(options, _reader, effectiveClock);
    }

    public Task<Result<Provider>> CreateProviderAsync(Provider provider, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _providers.CreateProviderAsync(provider, accessContext, cancellationToken);

    public Task<Result<Provider>> GetProviderAsync(ProviderId providerId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _providers.GetProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<Provider>>> ListProvidersAsync(ResourceAccessContext accessContext, bool includeRetired = false, CancellationToken cancellationToken = default) =>
        _providers.ListProvidersAsync(accessContext, includeRetired, cancellationToken);

    public Task<Result<Provider>> UpdateProviderAsync(Provider provider, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _providers.UpdateProviderAsync(provider, accessContext, cancellationToken);

    public Task<Result<Provider>> DeleteProviderAsync(ProviderId providerId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _providers.DeleteProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<Provider>> ReactivateProviderAsync(ProviderId providerId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _providers.ReactivateProviderAsync(providerId, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> CreateProviderAccountAsync(ProviderAccount account, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _accounts.CreateProviderAccountAsync(account, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> GetProviderAccountAsync(ProviderAccountId providerAccountId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _accounts.GetProviderAccountAsync(providerAccountId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<ProviderAccount>>> ListProviderAccountsAsync(ProviderId providerId, ResourceAccessContext accessContext, bool includeRetired = false, CancellationToken cancellationToken = default) =>
        _accounts.ListProviderAccountsAsync(providerId, accessContext, includeRetired, cancellationToken);

    public Task<Result<ProviderAccount>> UpdateProviderAccountAsync(ProviderAccount account, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _accounts.UpdateProviderAccountAsync(account, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> DeleteProviderAccountAsync(ProviderAccountId providerAccountId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _accounts.DeleteProviderAccountAsync(providerAccountId, accessContext, cancellationToken);

    public Task<Result<ProviderAccount>> ReactivateProviderAccountAsync(ProviderAccountId providerAccountId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _accounts.ReactivateProviderAccountAsync(providerAccountId, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> CreateExecutionTargetAsync(ExecutionTarget target, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _executionTargets.CreateExecutionTargetAsync(target, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> GetExecutionTargetAsync(ExecutionTargetId executionTargetId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _executionTargets.GetExecutionTargetAsync(executionTargetId, accessContext, cancellationToken);

    public Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(ResourceAccessContext accessContext, bool includeRetired = false, CancellationToken cancellationToken = default) =>
        _executionTargets.ListExecutionTargetsAsync(accessContext, includeRetired, cancellationToken);

    public Task<Result<IReadOnlyList<ExecutionTarget>>> ListExecutionTargetsAsync(ProviderAccountId providerAccountId, ResourceAccessContext accessContext, bool includeRetired = false, CancellationToken cancellationToken = default) =>
        _executionTargets.ListExecutionTargetsAsync(providerAccountId, accessContext, includeRetired, cancellationToken);

    public Task<Result<ExecutionTarget>> UpdateExecutionTargetAsync(ExecutionTarget target, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _executionTargets.UpdateExecutionTargetAsync(target, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> DeleteExecutionTargetAsync(ExecutionTargetId executionTargetId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _executionTargets.DeleteExecutionTargetAsync(executionTargetId, accessContext, cancellationToken);

    public Task<Result<ExecutionTarget>> ReactivateExecutionTargetAsync(ExecutionTargetId executionTargetId, ResourceAccessContext accessContext, CancellationToken cancellationToken = default) =>
        _executionTargets.ReactivateExecutionTargetAsync(executionTargetId, accessContext, cancellationToken);
}