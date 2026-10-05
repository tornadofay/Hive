using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderCompletionIntegrationTests
{
    [Fact]
    public void AgentExecutionRequest_RejectsPricingEvidenceForDifferentModel()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var agent = new AgentFactory(
                new AllowBaseAgentCreationAuthorizer())
            .Create<Agent>(
                new AgentDefinition(
                    "provider-completion-contract-agent",
                    "Provider Completion Contract Agent"),
                new AgentCreationContext(context))
            .Value!;

        var runtime = agent.CreateRuntimeInstance();
        var target = CreateTarget(
            ProviderId.New(),
            ProviderAccountId.New(),
            new Uri("https://example.invalid/v1/"),
            "target-model",
            context,
            DateTimeOffset.UtcNow);

        var pricing = new ExecutionPricingEvidence(
            "different-model",
            new ProviderModelPricing(
                [
                    new ProviderModelPrice(
                        "input_token",
                        0.35m,
                        "USD",
                        1_000_000m)
                ]),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(10));

        Assert.Throws<ArgumentException>(
            () => new AgentExecutionRequest(
                agent,
                runtime,
                target,
                context,
                "Model mismatch.",
                pricingEvidence: pricing));
    }

    [Fact]
    public async Task ExecuteConfiguredAgentAsync_UsesFreshCachedPricingWithoutDiscoveringDuringExecution()
    {
        var database = await PrepareDatabase("Hive_Test_ProviderCompletionIntegration");
        var eventStore = new SqlEventPersistenceStore(database.Options);

        var clock = new FixedClock(
            new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero));
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var provider = CreateProvider(context, clock.UtcNow);
        var account = CreateAccount(provider.Id, context, clock.UtcNow);
        var target = CreateTarget(
            provider.Id,
            account.Id,
            new Uri("https://example.invalid/v1/"),
            "integration-model",
            context,
            clock.UtcNow);

        var pricing = new ProviderModelPricing(
            [
                new ProviderModelPrice(
                    "input_token",
                    0.35m,
                    "USD",
                    1_000_000m),
                new ProviderModelPrice(
                    "output_token",
                    1.50m,
                    "USD",
                    1_000_000m)
            ],
            explicitFreeEvidence: false,
            variants:
            [
                new ProviderModelPricingVariant(
                    "batch",
                    [
                        new ProviderModelPrice(
                            "input_token",
                            0.175m,
                            "USD",
                            1_000_000m),
                        new ProviderModelPrice(
                            "output_token",
                            0.75m,
                            "USD",
                            1_000_000m)
                    ],
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["mode"] = "batch"
                    })
            ]);

        var discovery = new StaticProviderDiscovery(
            clock,
            provider.Id,
            account.Id,
            target.Endpoint,
            target.Model!,
            pricing);

        using var httpClient = new HttpClient(
            new StaticHttpMessageHandler(
                """{"id":"chatcmpl-provider-completion","model":"integration-model","usage":{"prompt_tokens":120,"completion_tokens":45,"total_tokens":165},"choices":[{"message":{"role":"assistant","content":"integration complete"}}]}"""));

        var execution = new AgentExecutionService(
            new HiveEventPersistenceComposition(eventStore),
            httpClient,
            TimeSpan.FromSeconds(5),
            clock);

        var providerStore = new SqlProviderResourceStore(database.Options);
        var agentStore = new SqlAgentDefinitionResourceStore(database.Options);
        var workItemStore = new SqlWorkItemResourceStore(database.Options);
        var facade = new HiveManagementFacade(
            providerStore,
            agentStore,
            workItemStore,
            agentExecution: execution,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        var createdProvider = await facade.CreateProviderAsync(provider, context);
        Assert.True(createdProvider.IsSuccess, createdProvider.Error?.Message);

        var createdAccount = await facade.CreateProviderAccountAsync(account, context);
        Assert.True(createdAccount.IsSuccess, createdAccount.Error?.Message);

        var createdTarget = await facade.CreateExecutionTargetAsync(target, context);
        Assert.True(createdTarget.IsSuccess, createdTarget.Error?.Message);

        var discovered = await facade.GetProviderDiscoveryAsync(
            provider.Id,
            account.Id,
            target.Endpoint,
            context);
        Assert.True(discovered.IsSuccess, discovered.Error?.Message);
        Assert.Equal(1, discovery.CallCount);

        var definition = CreateAgentDefinition(
            target.Id,
            context,
            clock.UtcNow);
        var createdDefinition = await facade.CreateAgentDefinitionAsync(
            definition,
            context);
        Assert.True(createdDefinition.IsSuccess, createdDefinition.Error?.Message);

        var result = await facade.ExecuteConfiguredAgentAsync(
            definition.Id,
            context,
            "Use the cached provider pricing evidence.");

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(TokenUsageEvidence.Actual, result.Value!.Usage.Evidence);
        Assert.Equal(120, result.Value.Usage.InputTokenCount);
        Assert.NotNull(result.Value.PricingEvidence);
        Assert.Equal("integration-model", result.Value.PricingEvidence!.ModelId);
        Assert.False(result.Value.PricingEvidence.IsStale(clock.UtcNow));
        Assert.Equal(0.35m, result.Value.PricingEvidence.Pricing.Prices.Single(
            price => price.BillingUnit == "input_token").Price);
        Assert.Equal(1.50m, result.Value.PricingEvidence.Pricing.Prices.Single(
            price => price.BillingUnit == "output_token").Price);
        Assert.Single(result.Value.PricingEvidence.Pricing.Variants);

        var events = await eventStore.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                result.Value.Execution.Id.Value));
        Assert.True(events.IsSuccess, events.Error?.Message);

        var succeeded = events.Value!.Single(
            item => item.Envelope.EventType.Value == "agent.execution.succeeded");
        Assert.Equal(2, succeeded.Envelope.PayloadSchemaVersion.Value);

        var usagePayload = succeeded.Envelope.Payload.GetProperty("usage");
        var pricingPayload = usagePayload.GetProperty("pricingEvidence");
        Assert.Equal(
            "integration-model",
            pricingPayload.GetProperty("modelId").GetString());
        Assert.False(
            pricingPayload.GetProperty("explicitFreeEvidence").GetBoolean());

        var persistedInputPrice = pricingPayload
            .GetProperty("prices")
            .EnumerateArray()
            .Single(item => item.GetProperty("billingUnit").GetString() == "input_token");
        Assert.Equal(0.35m, persistedInputPrice.GetProperty("price").GetDecimal());
        Assert.Equal(
            1_000_000m,
            persistedInputPrice.GetProperty("unitQuantity").GetDecimal());

        var persistedVariant = pricingPayload
            .GetProperty("variants")
            .EnumerateArray()
            .Single();
        Assert.Equal("batch", persistedVariant.GetProperty("key").GetString());
        Assert.Equal(
            "batch",
            persistedVariant.GetProperty("conditions").GetProperty("mode").GetString());
    }


    [Fact]
    public async Task ExecuteAsync_StalePricingEvidence_IsNotPersistedAsExecutionEvidence()
    {
        var database = await PrepareDatabase("Hive_Test_ProviderCompletionStalePricing");
        var eventStore = new SqlEventPersistenceStore(database.Options);

        var clock = new FixedClock(
            new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var target = CreateTarget(
            ProviderId.New(),
            ProviderAccountId.New(),
            new Uri("https://example.invalid/v1/"),
            "stale-model",
            context,
            clock.UtcNow);

        var pricing = new ProviderModelPricing(
            [
                new ProviderModelPrice(
                    "input_token",
                    0.35m,
                    "USD",
                    1_000_000m),
                new ProviderModelPrice(
                    "output_token",
                    1.50m,
                    "USD",
                    1_000_000m)
            ]);
        var stalePricing = new ExecutionPricingEvidence(
            target.Model!,
            pricing,
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero));

        using var httpClient = new HttpClient(
            new StaticHttpMessageHandler(
                """{"id":"chatcmpl-stale-pricing","model":"stale-model","usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15},"choices":[{"message":{"role":"assistant","content":"execution completed"}}]}"""));
        var service = new AgentExecutionService(
            new HiveEventPersistenceComposition(eventStore),
            httpClient,
            TimeSpan.FromSeconds(5),
            clock);

        var agent = CreateAgent(context);
        var runtime = agent.CreateRuntimeInstance();

        var result = await service.ExecuteAsync(
            new AgentExecutionRequest(
                agent,
                runtime,
                target,
                context,
                "Execute without stale pricing.",
                pricingEvidence: stalePricing));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Null(result.Value!.PricingEvidence);

        var events = await eventStore.ReadEventsAsync(
            new ResourceReference(
                ResourceKind.Execution,
                result.Value.Execution.Id.Value));
        Assert.True(events.IsSuccess, events.Error?.Message);

        var succeeded = events.Value!.Single(
            item => item.Envelope.EventType.Value == "agent.execution.succeeded");
        var pricingEvidence = succeeded.Envelope.Payload
            .GetProperty("usage")
            .GetProperty("pricingEvidence");

        Assert.Equal(JsonValueKind.Null, pricingEvidence.ValueKind);
    }

    private static Provider CreateProvider(
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                ProviderId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "integration-provider",
            "Integration Provider",
            "openai-compatible");

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            "integration-account",
            "Integration Account");

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        Uri endpoint,
        string model,
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            "integration-target",
            "Integration Target",
            endpoint,
            model,
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);

    private static AgentDefinition CreateAgentDefinition(
        ExecutionTargetId targetId,
        ResourceAccessContext context,
        DateTimeOffset now) =>
        new(
            new ResourceEnvelope<AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                AgentDefinitionId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            "provider-completion-agent",
            "Provider Completion Agent",
            AgentGeneration.Base,
            targetId);

    private static async Task<PersistenceTestDatabase> PrepareDatabase(string name)
    {
        var database = new PersistenceTestDatabase(name);
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(
            migration.IsSuccess,
            migration.Error is null
                ? "Migration failed without an error."
                : $"Migration failed: {migration.Error.Code} [{migration.Error.Category}] {migration.Error.Message}");

        return database;
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class StaticProviderDiscovery : IProviderCapabilityDiscovery
    {
        private readonly IClock _clock;
        private readonly ProviderId _providerId;
        private readonly ProviderAccountId _accountId;
        private readonly Uri _endpoint;
        private readonly string _modelId;
        private readonly ProviderModelPricing _pricing;

        public StaticProviderDiscovery(
            IClock clock,
            ProviderId providerId,
            ProviderAccountId accountId,
            Uri endpoint,
            string modelId,
            ProviderModelPricing pricing)
        {
            _clock = clock;
            _providerId = providerId;
            _accountId = accountId;
            _endpoint = endpoint;
            _modelId = modelId;
            _pricing = pricing;
        }

        public int CallCount { get; private set; }

        public Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;

            var now = _clock.UtcNow;
            var snapshot = new ProviderDiscoverySnapshot(
                _providerId,
                _accountId,
                _endpoint,
                new ProviderOperationalMetadata(
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Unknown,
                    now,
                    now.AddMinutes(10)),
                ProviderDiscoveryState.Supported,
                [
                    new ProviderModelMetadata(
                        modelId: _modelId,
                        ownedBy: "integration-test",
                        createdAtUtc: null,
                        availability: ProviderAvailabilityStatus.Available,
                        health: ProviderHealthStatus.Unknown,
                        discoveredCapabilities: [
                            new CapabilityStateEntry(
                                new CapabilityKey("text.generate"),
                                CapabilityState.Supported)
                        ],
                        pricing: _pricing,
                        observedAtUtc: now,
                        staleAfterUtc: now.AddMinutes(10))
                ]);

            return Task.FromResult(
                Result<ProviderDiscoverySnapshot>.Success(snapshot));
        }
    }

    private sealed class StaticHttpMessageHandler : HttpMessageHandler
    {
        private readonly string _responseBody;

        public StaticHttpMessageHandler(string responseBody) =>
            _responseBody = responseBody;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    _responseBody,
                    Encoding.UTF8,
                    "application/json")
            };
            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue("application/json");
            return Task.FromResult(response);
        }
    }
}
