using Hive.Core;

namespace Hive.Providers.OpenAICompatible;

public sealed class OpenAICompatibleProviderCapabilityDiscovery :
    IProviderCapabilityDiscovery
{
    private static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromSeconds(30);

    private static readonly TimeSpan DefaultFreshness =
        TimeSpan.FromMinutes(5);

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _timeout;
    private readonly TimeSpan _freshness;
    private readonly IClock _clock;

    public OpenAICompatibleProviderCapabilityDiscovery(
        HttpClient httpClient,
        TimeSpan? timeout = null,
        TimeSpan? freshness = null,
        IClock? clock = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        _timeout = timeout ?? DefaultTimeout;
        if (_timeout <= TimeSpan.Zero || _timeout > TimeSpan.FromMinutes(10))
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeout),
                _timeout,
                "Provider discovery timeout must be greater than zero and no more than ten minutes.");
        }

        _freshness = freshness ?? DefaultFreshness;
        if (_freshness <= TimeSpan.Zero || _freshness > TimeSpan.FromDays(1))
        {
            throw new ArgumentOutOfRangeException(
                nameof(freshness),
                _freshness,
                "Discovery freshness must be greater than zero and no more than one day.");
        }

        _clock = clock ?? SystemClock.Instance;
    }

    public async Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
        Provider provider,
        ProviderAccount account,
        ExecutionTarget target,
        SecretMaterial? credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(account);
        ArgumentNullException.ThrowIfNull(target);

        if (account.ProviderId != provider.Id)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Validation(
                    "hive.provider.discovery.account-provider-mismatch",
                    "The provider account does not belong to the supplied provider."));
        }

        if (target.ProviderId != provider.Id)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Validation(
                    "hive.provider.discovery.target-provider-mismatch",
                    "The execution target does not belong to the supplied provider."));
        }

        if (target.ProviderAccountId != account.Id)
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Validation(
                    "hive.provider.discovery.target-account-mismatch",
                    "The execution target does not belong to the supplied provider account."));
        }

        if (!string.Equals(
                provider.TransportKind,
                "openai-compatible",
                StringComparison.OrdinalIgnoreCase))
        {
            return Result<ProviderDiscoverySnapshot>.Failure(
                Error.Unsupported(
                    "hive.provider.discovery.unsupported-transport",
                    $"Provider transport '{provider.TransportKind}' does not support this discovery implementation."));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var options = new OpenAICompatibleProviderOptions(
            target.Endpoint,
            credential,
            _timeout);

        var adapter = new OpenAICompatibleProviderAdapter(
            _httpClient,
            options);

        var catalog = await adapter
            .ListModelsAsync(cancellationToken)
            .ConfigureAwait(false);

        if (catalog.IsFailure)
        {
            if (catalog.Error?.Code ==
                "hive.provider.openai-compatible.model-enumeration-unsupported")
            {
                var observedAt = _clock.UtcNow;

                return Result<ProviderDiscoverySnapshot>.Success(
                    new ProviderDiscoverySnapshot(
                        provider.Id,
                        account.Id,
                        target.Endpoint,
                        new ProviderOperationalMetadata(
                            ProviderAvailabilityStatus.Unknown,
                            ProviderHealthStatus.Unknown,
                            observedAt,
                            observedAt.Add(_freshness),
                            null),
                        ProviderDiscoveryState.Unsupported,
                        Array.Empty<ProviderModelMetadata>()));
            }

            return Result<ProviderDiscoverySnapshot>.Failure(
                catalog.Error!);
        }

        var now = _clock.UtcNow;

        var models = catalog.Value!.Models
            .Select(model =>
                new ProviderModelMetadata(
                    model.Id,
                    model.OwnedBy,
                    model.CreatedAtUtc,
                    model.Availability,
                    model.Health,
                    model.Capabilities
                        .Select(capability =>
                            new CapabilityStateEntry(
                                new CapabilityKey(capability.Key),
                                capability.State))
                        .ToArray()))
            .ToArray();

        return Result<ProviderDiscoverySnapshot>.Success(
            new ProviderDiscoverySnapshot(
                provider.Id,
                account.Id,
                target.Endpoint,
                new ProviderOperationalMetadata(
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Unknown,
                    now,
                    now.Add(_freshness),
                    catalog.Value.RateLimitRemaining),
                ProviderDiscoveryState.Supported,
                models));
    }
}
