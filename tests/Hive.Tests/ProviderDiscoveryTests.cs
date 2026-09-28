using System.Net;
using System.Text;
using Hive.Core;
using Hive.Providers.OpenAICompatible;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderDiscoveryTests
{
    [Theory]
    [InlineData(ProviderDiscoveryState.Unsupported)]
    [InlineData(ProviderDiscoveryState.Unknown)]
    public void DiscoverySnapshot_NonSupportedEnumerationCannotContainModels(
        ProviderDiscoveryState state)
    {
        var target = CreateTarget(Array.Empty<CapabilityStateEntry>());
        var model = CreateModel(
            target,
            Capability("vision", CapabilityState.Supported));

        Assert.Throws<ArgumentException>(
            () => new ProviderDiscoverySnapshot(
                target.ProviderId,
                target.ProviderAccountId,
                target.Endpoint,
                new ProviderOperationalMetadata(
                    ProviderAvailabilityStatus.Unknown,
                    ProviderHealthStatus.Unknown,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(5)),
                state,
                [model]));
    }

    [Fact]
    public void CapabilityResolver_ConfiguredStateOverridesDiscoveredState()
    {
        var target = CreateTarget(
            [
                Capability("vision", CapabilityState.Unsupported)
            ]);

        var discovery = CreateDiscovery(
            target,
            DateTimeOffset.UtcNow.AddMinutes(5),
            Capability("vision", CapabilityState.Supported),
            Capability("tool.calling", CapabilityState.Supported));

        var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
            target,
            discovery,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            CapabilityState.Unsupported,
            effective.Single(item => item.Capability == new CapabilityKey("vision")).State);
        Assert.Equal(
            CapabilityState.Supported,
            effective.Single(item => item.Capability == new CapabilityKey("tool.calling")).State);
        Assert.Single(target.Capabilities);
    }

    [Fact]
    public void CapabilityResolver_ConfiguredOverrideUsesCanonicalCapabilityIdentity()
    {
        var target = CreateTarget(
        [
            Capability("VISION", CapabilityState.Unsupported)
        ]);

        var discovery = CreateDiscovery(
            target,
            DateTimeOffset.UtcNow.AddMinutes(5),
            Capability("vision", CapabilityState.Supported));

        var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
            target,
            discovery,
            DateTimeOffset.UtcNow);

        var vision = Assert.Single(
            effective,
            capability => capability.Capability == new CapabilityKey("vision"));

        Assert.Equal(
            CapabilityState.Unsupported,
            vision.State);
        Assert.Single(effective);
    }

    [Fact]
    public void CapabilityKey_CanonicalizesSemanticIdentity()
    {
        var upper = new CapabilityKey("  VISION  ");
        var lower = new CapabilityKey("vision");

        Assert.Equal("vision", upper.Value);
        Assert.Equal(lower, upper);
    }

    [Theory]
    [InlineData(
        "https://example.test/v1/?mode=FAST",
        "HTTPS://EXAMPLE.TEST/v1/?mode=FAST",
        true)]
    [InlineData(
        "https://example.test/v1/?mode=FAST",
        "https://example.test/v1/?mode=fast",
        false)]
    [InlineData(
        "https://example.test:443/v1/",
        "https://example.test:443/v1/",
        true)]
    [InlineData(
        "https://example.test/v1/",
        "https://example.test:8443/v1/",
        false)]
    public void ProviderEndpointIdentity_AppliesAuthorityAndPathQuerySemantics(
        string left,
        string right,
        bool expected)
    {
        Assert.Equal(
            expected,
            ProviderEndpointIdentity.Equals(
                new Uri(left),
                new Uri(right)));
    }

    [Fact]
    public void CapabilityResolver_DifferentEndpointPathCaseDoesNotMatch()
    {
        var target = CreateTarget(Array.Empty<CapabilityStateEntry>());
        var discovery = new ProviderDiscoverySnapshot(
            target.ProviderId,
            target.ProviderAccountId,
            new Uri("https://example.test/V1/"),
            new ProviderOperationalMetadata(
                ProviderAvailabilityStatus.Available,
                ProviderHealthStatus.Unknown,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(5)),
            ProviderDiscoveryState.Supported,
            [
                CreateModel(
                    target,
                    Capability("vision", CapabilityState.Supported))
            ]);

        var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
            target,
            discovery,
            DateTimeOffset.UtcNow);

        Assert.Empty(effective);
    }

    [Fact]
    public void CapabilityResolver_StaleDiscoveryDoesNotProvideEffectiveCapability()
    {
        var target = CreateTarget(Array.Empty<CapabilityStateEntry>());
        var discovery = CreateDiscovery(
            target,
            DateTimeOffset.UtcNow.AddSeconds(-1),
            Capability("vision", CapabilityState.Supported));

        var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
            target,
            discovery,
            DateTimeOffset.UtcNow);

        Assert.Empty(effective);
    }

    [Fact]
    public void CapabilityResolver_MatchingFreshModelAddsDiscoveredCapabilities()
    {
        var target = CreateTarget(
            [
                Capability("text.generate", CapabilityState.Supported)
            ]);

        var discovery = CreateDiscovery(
            target,
            DateTimeOffset.UtcNow.AddMinutes(5),
            Capability("vision", CapabilityState.Supported));

        var effective = ExecutionTargetCapabilityResolver.ResolveCapabilities(
            target,
            discovery,
            DateTimeOffset.UtcNow);

        Assert.Equal(
            CapabilityState.Supported,
            effective.Single(item => item.Capability == new CapabilityKey("vision")).State);
        Assert.Equal(
            CapabilityState.Supported,
            effective.Single(item => item.Capability == new CapabilityKey("text.generate")).State);
    }

    [Fact]
    public void Selector_UsesEffectiveCapabilityOverridesWithoutMutatingTarget()
    {
        var target = CreateTarget(
            [
                Capability("vision", CapabilityState.Unknown)
            ]);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [target],
                [
                    new CapabilityRequirement(
                        new CapabilityKey("vision"),
                        CapabilityRequirementKind.Required)
                ]),
            new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>
            {
                [target.Id] =
                [
                    Capability("vision", CapabilityState.Supported)
                ]
            });

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            CapabilityState.Unknown,
            target.Capabilities.Single().State);
        Assert.Equal(target.Id, result.Value!.SelectedTarget.Id);
    }

    [Fact]
    public void Selector_RejectsInvalidCapabilityOverrideTarget()
    {
        var target = CreateTarget(
            [Capability("vision", CapabilityState.Supported)]);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [target],
                Array.Empty<CapabilityRequirement>()),
            new Dictionary<ExecutionTargetId, IReadOnlyList<CapabilityStateEntry>>
            {
                [ExecutionTargetId.New()] =
                [
                    Capability("vision", CapabilityState.Supported)
                ]
            });

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Validation, result.Error!.Category);
    }

    [Fact]
    public async Task Adapter_ListModels_NormalizesExplicitCapabilitiesAndMetadata()
    {
        var handler = new RecordingHandler(
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "data": [
                            {
                              "id": "vision-model",
                              "created": 1700000000,
                              "owned_by": "example",
                              "available": true,
                              "health": "healthy",
                              "capabilities": {
                                "vision": true,
                                "tool_calling": false,
                                "structured_output": "supported",
                                "vendor_only_capability": true
                              }
                            }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
                response.Headers.Add("x-ratelimit-remaining-requests", "17");
                return response;
            });

        using var client = new HttpClient(handler);
        using var credential = SecretMaterial.Create("test-secret");

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/"),
                credential));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("/v1/models", handler.Request?.RequestUri?.AbsolutePath);
        Assert.Equal(HttpMethod.Get, handler.Request?.Method);
        Assert.Equal("Bearer", handler.Request?.Headers.Authorization?.Scheme);
        Assert.Equal("test-secret", handler.Request?.Headers.Authorization?.Parameter);
        Assert.Equal(17, result.Value!.RateLimitRemaining);

        var model = Assert.Single(result.Value.Models);
        Assert.Equal("vision-model", model.Id);
        Assert.Equal("example", model.OwnedBy);
        Assert.Equal(
            DateTimeOffset.FromUnixTimeSeconds(1700000000),
            model.CreatedAtUtc);
        Assert.Equal(ProviderAvailabilityStatus.Available, model.Availability);
        Assert.Equal(ProviderHealthStatus.Healthy, model.Health);
        Assert.Equal(
            CapabilityState.Supported,
            model.Capabilities.Single(item => item.Key == "vision").State);
        Assert.Equal(
            CapabilityState.Unsupported,
            model.Capabilities.Single(item => item.Key == "tool.calling").State);
        Assert.Equal(
            CapabilityState.Supported,
            model.Capabilities.Single(item => item.Key == "structured.output").State);
        Assert.DoesNotContain(
            model.Capabilities,
            item => item.Key == "vendor_only_capability");
    }

    [Fact]
    public async Task Adapter_ListModels_ConflictingNormalizedCapabilitySignalsBecomeUnknown()
    {
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": [
                        {
                          "id": "conflicting-model",
                          "supports_vision": false,
                          "supports_tools": false,
                          "supports_function_calling": true,
                          "capabilities": {
                            "vision": true
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        using var client = new HttpClient(handler);
        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsSuccess, result.Error?.Message);

        var model = Assert.Single(result.Value!.Models);
        Assert.Equal(
            CapabilityState.Unknown,
            model.Capabilities.Single(
                item => item.Key == "vision").State);
        Assert.Equal(
            CapabilityState.Unknown,
            model.Capabilities.Single(
                item => item.Key == "tool.calling").State);
    }

    [Fact]
    public async Task OpenAICompatibleDiscovery_MapsCatalogToProviderNeutralSnapshot()
    {
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "data": [
                        {
                          "id": "vision-model",
                          "available": false,
                          "health": "degraded",
                          "capabilities": {
                            "vision": true
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            });

        using var client = new HttpClient(handler);
        var discovery = new OpenAICompatibleProviderCapabilityDiscovery(
            client,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(2));

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

        var provider = new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "example-provider",
            "Provider",
            "openai-compatible");

        var account = new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            "account",
            "Account",
            "example");

        var target = new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            account.Id,
            "target",
            "Target",
            new Uri("https://example.test/v1/"),
            "vision-model",
            null,
            Array.Empty<CapabilityStateEntry>());

        var result = await discovery.DiscoverAsync(
            provider,
            account,
            target,
            null);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            ProviderDiscoveryState.Supported,
            result.Value!.ModelEnumerationState);

        var model = Assert.Single(result.Value.Models);
        Assert.Equal("vision-model", model.ModelId);
        Assert.Equal(
            ProviderAvailabilityStatus.Unavailable,
            model.Availability);
        Assert.Equal(
            ProviderHealthStatus.Degraded,
            model.Health);
        Assert.Equal(
            CapabilityState.Supported,
            model.DiscoveredCapabilities.Single().State);
        Assert.Equal(
            TimeSpan.FromMinutes(2),
            result.Value.Operational.StaleAfterUtc -
            result.Value.Operational.ObservedAtUtc);
    }

    [Fact]
    public async Task OpenAICompatibleDiscovery_UnsupportedEndpointDoesNotFabricateModels()
    {
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var client = new HttpClient(handler);
        var discovery = new OpenAICompatibleProviderCapabilityDiscovery(
            client,
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(2));

        var target = CreateTarget(Array.Empty<CapabilityStateEntry>());
        var provider = CreateProviderFor(target);
        var account = CreateAccountFor(provider, target);

        var result = await discovery.DiscoverAsync(
            provider,
            account,
            CreateTargetFor(provider, account, target),
            null);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(
            ProviderDiscoveryState.Unsupported,
            result.Value!.ModelEnumerationState);
        Assert.Equal(
            ProviderAvailabilityStatus.Unknown,
            result.Value.Operational.Availability);
        Assert.Equal(
            ProviderHealthStatus.Unknown,
            result.Value.Operational.Health);
        Assert.Empty(result.Value.Models);
    }

    private static Provider CreateProviderFor(ExecutionTarget target)
    {
        var now = DateTimeOffset.UtcNow;

        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                target.Resource.Owner,
                target.Resource.Scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    target.Resource.Provenance.CreatedBy,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "example-provider",
            "Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateAccountFor(
        Provider provider,
        ExecutionTarget target)
    {
        var now = DateTimeOffset.UtcNow;

        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                target.Resource.Owner,
                target.Resource.Scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    target.Resource.Provenance.CreatedBy,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            "account",
            "Account",
            "example");
    }

    private static ExecutionTarget CreateTargetFor(
        Provider provider,
        ProviderAccount account,
        ExecutionTarget source)
    {
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                source.Resource.Owner,
                source.Resource.Scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    source.Resource.Provenance.CreatedBy,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            provider.Id,
            account.Id,
            "target",
            "Target",
            source.Endpoint,
            source.Model,
            source.Deployment,
            source.Capabilities);
    }

    [Fact]
    public async Task Adapter_ListModels_UnsupportedEndpointIsTyped()
    {
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound));

        using var client = new HttpClient(handler);
        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Unsupported, result.Error!.Category);
        Assert.Equal(
            "hive.provider.openai-compatible.model-enumeration-unsupported",
            result.Error.Code);
    }

    [Fact]
    public async Task Adapter_ListModels_MalformedCatalogIsTypedWithoutResponseEcho()
    {
        const string secret = "super-secret-value";

        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"data":[{"id":"{{secret}}"}]""",
                    Encoding.UTF8,
                    "application/json")
            });

        using var client = new HttpClient(handler);
        using var credential = SecretMaterial.Create(secret);

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/"),
                credential));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Serialization, result.Error!.Category);
        Assert.DoesNotContain(secret, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Adapter_ListModels_CallerCancellationIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var handler = new RecordingHandler(
            _ => throw new InvalidOperationException("Handler must not be reached."));

        using var client = new HttpClient(handler);
        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => adapter.ListModelsAsync(cancellation.Token));

        Assert.Null(handler.Request);
    }

    [Fact]
    public async Task Adapter_ListModels_TimeoutIsTyped()
    {
        using var client = new HttpClient(
            new BlockingHandler());

        var adapter = new OpenAICompatibleProviderAdapter(
            client,
            new OpenAICompatibleProviderOptions(
                new Uri("https://example.test/v1/"),
                timeout: TimeSpan.FromMilliseconds(20)));

        var result = await adapter.ListModelsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorCategory.Timeout, result.Error!.Category);
        Assert.Equal(
            "hive.provider.openai-compatible.timeout",
            result.Error.Code);
    }

    private static ProviderModelMetadata CreateModel(
        ExecutionTarget target,
        params CapabilityStateEntry[] capabilities) =>
        new(
            target.Model ?? target.Deployment!,
            "example",
            null,
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            capabilities);

    private static ProviderDiscoverySnapshot CreateDiscovery(
        ExecutionTarget target,
        DateTimeOffset staleAfterUtc,
        params CapabilityStateEntry[] capabilities)
    {
        var observedAt = staleAfterUtc.AddMinutes(-1);

        return new ProviderDiscoverySnapshot(
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            new ProviderOperationalMetadata(
                ProviderAvailabilityStatus.Available,
                ProviderHealthStatus.Unknown,
                observedAt,
                staleAfterUtc),
            ProviderDiscoveryState.Supported,
            [
                CreateModel(target, capabilities)
            ]);
    }

    private static ExecutionTarget CreateTarget(
        IReadOnlyList<CapabilityStateEntry> capabilities)
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            ProviderId.New(),
            ProviderAccountId.New(),
            "target",
            "Target",
            new Uri("https://example.test/v1/"),
            "vision-model",
            null,
            capabilities);
    }

    private static CapabilityStateEntry Capability(
        string key,
        CapabilityState state) =>
        new(new CapabilityKey(key), state);

    private sealed class RecordingHandler :
        HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _factory;

        public RecordingHandler(
            Func<HttpRequestMessage, HttpResponseMessage> factory)
        {
            _factory = factory;
        }

        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(_factory(request));
        }
    }

    private sealed class BlockingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
