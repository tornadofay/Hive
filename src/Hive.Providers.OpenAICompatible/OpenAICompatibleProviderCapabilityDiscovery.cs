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

        var endpointResult = ResolveModelCatalogEndpoint(
            provider.Key,
            target.Endpoint);

        if (endpointResult.IsFailure)
            return Result<ProviderDiscoverySnapshot>.Failure(
                endpointResult.Error!);

        var endpoint = endpointResult.Value!;

        var options = new OpenAICompatibleProviderOptions(
            target.Endpoint,
            credential,
            _timeout);

        var adapter = new OpenAICompatibleProviderAdapter(
            _httpClient,
            options);

        var catalog = await adapter
            .ListModelsAsync(
                endpoint.Uri,
                endpoint.Format,
                cancellationToken)
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

        var staleAfter = now.Add(_freshness);

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
                        .ToArray(),
                    model.InputModalities,
                    model.OutputModalities,
                    model.DisplayName,
                    model.Description,
                    model.Family,
                    model.ModelType,
                    model.Category,
                    model.Version,
                    model.OperationalState,
                    model.ThinkingOptions,
                    model.DefaultThinkingLevel,
                    model.Limits,
                    model.Pricing,
                    model.ExtensionData,
                    now,
                    staleAfter))
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
                    staleAfter,
                    catalog.Value.RateLimitRemaining),
                ProviderDiscoveryState.Supported,
                models));
    }

    private static Result<ModelCatalogEndpoint> ResolveModelCatalogEndpoint(
        string providerKey,
        Uri baseEndpoint)
    {
        ArgumentNullException.ThrowIfNull(baseEndpoint);

        var key = providerKey?.Trim().ToLowerInvariant();

        return key switch
        {
            "cerebras" => Result<ModelCatalogEndpoint>.Success(
                new ModelCatalogEndpoint(
                    BuildCerebrasModelsUri(baseEndpoint),
                    OpenAICompatibleModelCatalogFormat.CerebrasOpenRouter)),

            "google-gemini" => Result<ModelCatalogEndpoint>.Success(
                new ModelCatalogEndpoint(
                    BuildGeminiModelsUri(baseEndpoint),
                    OpenAICompatibleModelCatalogFormat.Gemini)),

            "lm-studio" => Result<ModelCatalogEndpoint>.Success(
                new ModelCatalogEndpoint(
                    BuildLmStudioModelsUri(baseEndpoint),
                    OpenAICompatibleModelCatalogFormat.LmStudio)),

            "ollama" => Result<ModelCatalogEndpoint>.Success(
                new ModelCatalogEndpoint(
                    BuildOllamaModelsUri(baseEndpoint),
                    OpenAICompatibleModelCatalogFormat.Ollama)),

            "cloudflare" => BuildCloudflareModelsEndpoint(baseEndpoint),

            "openrouter" => Result<ModelCatalogEndpoint>.Success(
                new ModelCatalogEndpoint(
                    BuildModelsUri(baseEndpoint),
                    OpenAICompatibleModelCatalogFormat.OpenRouter)),

            _ => Result<ModelCatalogEndpoint>.Success(
                new ModelCatalogEndpoint(
                    BuildModelsUri(baseEndpoint),
                    OpenAICompatibleModelCatalogFormat.Standard))
        };
    }

    private static Uri BuildModelsUri(Uri baseEndpoint)
    {
        var path = baseEndpoint.GetLeftPart(UriPartial.Path);

        if (!path.EndsWith("/", StringComparison.Ordinal))
            path += "/";

        return new Uri(
            path + "models",
            UriKind.Absolute);
    }

    private static Uri BuildCerebrasModelsUri(Uri baseEndpoint) =>
        new Uri(
            baseEndpoint.GetLeftPart(UriPartial.Authority) +
            "/public/v1/models?format=openrouter",
            UriKind.Absolute);

    private static Uri BuildGeminiModelsUri(Uri baseEndpoint)
    {
        var path = baseEndpoint.AbsolutePath.TrimEnd('/');

        var openAiSegment = path.LastIndexOf(
            "/openai",
            StringComparison.OrdinalIgnoreCase);

        var modelsPath = openAiSegment >= 0
            ? path[..openAiSegment] + "/models"
            : "/v1beta/models";

        var builder = new UriBuilder(baseEndpoint.Scheme, baseEndpoint.Host)
        {
            Port = baseEndpoint.IsDefaultPort ? -1 : baseEndpoint.Port,
            Path = modelsPath
        };

        return builder.Uri;
    }

    private static Uri BuildLmStudioModelsUri(Uri baseEndpoint) =>
        new Uri(
            baseEndpoint.GetLeftPart(UriPartial.Authority) +
            "/api/v1/models",
            UriKind.Absolute);

    private static Uri BuildOllamaModelsUri(Uri baseEndpoint) =>
        new Uri(
            baseEndpoint.GetLeftPart(UriPartial.Authority) +
            "/api/tags",
            UriKind.Absolute);

    private static Result<ModelCatalogEndpoint> BuildCloudflareModelsEndpoint(
        Uri baseEndpoint)
    {
        var segments = baseEndpoint.AbsolutePath
            .Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

        var accountIndex = Array.FindIndex(
            segments,
            segment => string.Equals(
                segment,
                "accounts",
                StringComparison.OrdinalIgnoreCase));

        if (accountIndex < 0 ||
            accountIndex + 1 >= segments.Length ||
            string.IsNullOrWhiteSpace(segments[accountIndex + 1]))
        {
            return Result<ModelCatalogEndpoint>.Failure(
                Error.Validation(
                    "hive.provider.discovery.cloudflare-account-endpoint-required",
                    "A Cloudflare Workers AI endpoint must include the account identifier in its path."));
        }

        var accountId = segments[accountIndex + 1];

        return Result<ModelCatalogEndpoint>.Success(
            new ModelCatalogEndpoint(
                new Uri(
                    baseEndpoint.GetLeftPart(UriPartial.Authority) +
                    $"/client/v4/accounts/{Uri.EscapeDataString(accountId)}/ai/models/search?format=openrouter&per_page=100",
                    UriKind.Absolute),
                OpenAICompatibleModelCatalogFormat.CloudflareOpenRouter));
    }

    private sealed record ModelCatalogEndpoint(
        Uri Uri,
        OpenAICompatibleModelCatalogFormat Format);

    
}
