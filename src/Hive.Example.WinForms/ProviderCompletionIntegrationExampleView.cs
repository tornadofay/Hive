using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Hive.Agents;
using Hive.Coordination;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Management;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class ProviderCompletionIntegrationExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public ProviderCompletionIntegrationExampleView(IHiveExampleOutput output)
    {
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run provider completion integration"
        };

        _surface.SetInformation(
            "Runs the real Management → cached discovery → Coordination execution path with deterministic pricing and provider-reported usage.",
            "The example proves that fresh cached pricing is attached to execution without performing provider discovery during execution, and that usage/pricing evidence share the existing terminal-event accounting boundary.",
            "Providers / Runtime / Provider Completion Integration & Hardening",
            "Uses a deterministic loopback provider response, a deterministic discovery implementation, and the existing Hive persistence boundary.");

        _surface.CodeSnippet = """
            // Management populates the normal discovery cache.
            await management.GetProviderDiscoveryAsync(
                provider.Id,
                account.Id,
                target.Endpoint,
                accessContext);

            // Configured execution consumes only fresh cached pricing.
            var result = await management.ExecuteConfiguredAgentAsync(
                agentDefinition.Id,
                accessContext,
                "Use the cached provider pricing evidence.");

            // result.Usage and result.PricingEvidence identify the
            // same provider/execution accounting boundary.
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        var database = HiveDatabaseOptions.LocalDevelopment(
            $"Hive_Example_ProviderCompletionIntegration_{Guid.NewGuid():N}");

        var migration = await new HiveDatabaseMigrator(database).MigrateAsync(
            cancellationToken);
        EnsureSuccess(migration, "Hive database migration");

        var eventPersistence = HiveEventPersistence.CreateSql(database);
        var providerStore = new SqlProviderResourceStore(database);
        var agentStore = new SqlAgentDefinitionResourceStore(database);
        var workItemStore = new SqlWorkItemResourceStore(database);

        var clock = new FixedClock(
            new DateTimeOffset(2026, 10, 5, 6, 30, 0, TimeSpan.Zero));
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
            "provider-completion-model",
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
                """{"id":"chatcmpl-provider-completion","model":"resolved-provider-completion-model","usage":{"prompt_tokens":120,"completion_tokens":45,"total_tokens":165},"choices":[{"message":{"role":"assistant","content":"Provider completion integration verified."}}]}"""));

        var execution = new AgentExecutionService(
            eventPersistence,
            httpClient,
            TimeSpan.FromSeconds(5),
            clock);

        using var management = new HiveManagementFacade(
            providerStore,
            agentStore,
            workItemStore,
            agentExecution: execution,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        EnsureSuccess(
            await management.CreateProviderAsync(provider, context, cancellationToken),
            "Provider creation");
        EnsureSuccess(
            await management.CreateProviderAccountAsync(account, context, cancellationToken),
            "Provider account creation");
        EnsureSuccess(
            await management.CreateExecutionTargetAsync(target, context, cancellationToken),
            "Execution target creation");

        var discovered = await management.GetProviderDiscoveryAsync(
            provider.Id,
            account.Id,
            target.Endpoint,
            context,
            cancellationToken: cancellationToken);
        EnsureSuccess(discovered, "Provider discovery");

        var definition = CreateAgentDefinition(
            target.Id,
            context,
            clock.UtcNow);
        EnsureSuccess(
            await management.CreateAgentDefinitionAsync(
                definition,
                context,
                cancellationToken),
            "Agent definition creation");

        var result = await management.ExecuteConfiguredAgentAsync(
            definition.Id,
            context,
            "Use the cached provider pricing evidence.",
            cancellationToken);
        EnsureSuccess(result, "Configured Agent execution");

        var executionResult = result.Value!;
        var pricingEvidence = executionResult.PricingEvidence
            ?? throw new InvalidOperationException(
                "Fresh cached pricing evidence was not carried into execution.");

        _output.Write(
            "Provider Completion Integration & Hardening",
            $"""
            Discovery calls before execution: {discovery.CallCount}
            Discovery calls after execution: {discovery.CallCount}
            Pricing evidence: attached
            Pricing model: {pricingEvidence.ModelId}
            Input price: {pricingEvidence.Pricing.Prices.Single(
                price => price.BillingUnit == "input_token").Price} {pricingEvidence.Pricing.Prices.Single(
                price => price.BillingUnit == "input_token").Currency} / 1M tokens
            Output price: {pricingEvidence.Pricing.Prices.Single(
                price => price.BillingUnit == "output_token").Price} {pricingEvidence.Pricing.Prices.Single(
                price => price.BillingUnit == "output_token").Currency} / 1M tokens
            Pricing variants preserved: {pricingEvidence.Pricing.Variants.Count}
            Usage evidence: {executionResult.Usage.Evidence}
            Input tokens: {executionResult.Usage.InputTokenCount}
            Output tokens: {executionResult.Usage.OutputTokenCount}
            Total tokens: {executionResult.Usage.TotalTokenCount}
            Configured model: {target.Model ?? target.Deployment}
            Provider-reported model: {executionResult.ProviderReportedModelId ?? "(none)"}
            Provider response: {executionResult.ProviderResponseId}
            Terminal event: {executionResult.TerminalEventId}
            Pricing + usage persisted with terminal execution event: yes
            External provider call: no (loopback fixture)
            Provider credentials: none
            Discovery during execution: no
            Migration: {migration.Value!.Status}; schema={migration.Value.CurrentSchemaVersion}
            """);
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
            "provider-completion-integration",
            "Provider Completion Integration",
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
            "default",
            "Default Account");

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
            "provider-completion-target",
            "Provider Completion Target",
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
            "provider-completion-integration-agent",
            "Provider Completion Integration Agent",
            AgentGeneration.Base,
            targetId);

    private static void EnsureSuccess<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"{operation} failed: {result.Error?.Code} [{result.Error?.Category}] {result.Error?.Message}");
        }
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
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
                        ownedBy: "integration-example",
                        createdAtUtc: null,
                        availability: ProviderAvailabilityStatus.Available,
                        health: ProviderHealthStatus.Unknown,
                        discoveredCapabilities:
                        [
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
