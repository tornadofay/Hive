using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using System.Reflection;
using System.Text.Json;

namespace Hive.Example.WinForms;

internal sealed class ProviderModelInformationExampleView : UserControl
{
    private readonly IHiveThemeManager _themeManager;
    private readonly IHiveExampleOutput _output;
    private readonly HiveExampleTestSurface _surface;
    private readonly ResourceAccessContext _context = new(
        DeploymentId.New(),
        TenantId.New(),
        PrincipalId.New());

    public ProviderModelInformationExampleView(
        IHiveThemeManager themeManager,
        IHiveExampleOutput output)
    {
        _themeManager = themeManager ?? throw new ArgumentNullException(nameof(themeManager));
        _output = output ?? throw new ArgumentNullException(nameof(output));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Open Model Information"
        };

        _surface.SetInformation(
            "Opens the real Hive Settings surface with a deterministic provider/model discovery fixture. Select Providers, then Model Information to inspect a complete normalized model profile without a vendor network call or real credential.",
            "The fixture demonstrates identity, modalities, known capability states, reasoning/thinking options, model-scoped limits, pricing/economic evidence, operational state, freshness, and bounded provider-specific evidence. The information is observational and does not create a durable Model resource.",
            "Providers / Target Selection / Capability Discovery / Provider / Model Information",
            "Uses a deterministic in-process Management facade fixture; no database, provider account, API key, or external network call is required.");

        _surface.CodeSnippet = """
            using var form = new HiveSettingsForm(
                managementFixture,
                context,
                themeManager,
                output);

            form.ShowDialog(owner);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);
        _themeManager.Apply(this);
    }

    private Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fixture = new ModelInformationFixture();
        using var form = new HiveSettingsForm(
            fixture.Management,
            fixture.Context,
            _themeManager,
            _output);

        form.ShowDialog(FindForm());

        _output.Write(
            "Provider / Model Information",
            """
            Hive Settings opened with deterministic discovery data.
            Select: Providers → Model Information
            Model fixture: rich-model
            Profile sections: Identity / Inputs / Outputs / Capabilities / Reasoning / Thinking / Limits / Pricing / Operational state / Additional provider information
            Credential: none
            Durable Model resource created: no
            External provider call: no
            """);

        return Task.CompletedTask;
    }

    private sealed class ModelInformationFixture
    {
        public ModelInformationFixture()
        {
            Context = new ResourceAccessContext(
                DeploymentId.New(),
                TenantId.New(),
                PrincipalId.New());

            var principal = Context.PrincipalId!.Value;
            var tenant = Context.TenantId!.Value;
            var created = new DateTimeOffset(
                2030,
                1,
                2,
                3,
                4,
                5,
                TimeSpan.Zero);

            Provider = new Provider(
                new ResourceEnvelope<ProviderId>(
                    ResourceKind.Provider,
                    ProviderId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        created,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(created)),
                "example-model-information-provider",
                "Example Model Information Provider",
                "openai-compatible");

            Account = new ProviderAccount(
                new ResourceEnvelope<ProviderAccountId>(
                    ResourceKind.ProviderAccount,
                    ProviderAccountId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        created,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(created)),
                Provider.Id,
                "example-model-information-account",
                "Example Model Information Account",
                "deterministic-fixture");

            Target = new ExecutionTarget(
                new ResourceEnvelope<ExecutionTargetId>(
                    ResourceKind.ExecutionTarget,
                    ExecutionTargetId.New(),
                    principal,
                    ResourceScope.Tenant(tenant),
                    ResourceVersion.Initial,
                    new ResourceProvenance(
                        principal,
                        created,
                        CorrelationId.New()),
                    ResourceLifecycle.Active(created)),
                Provider.Id,
                Account.Id,
                "example-model-information-target",
                "Example Model Information Target",
                new Uri("https://example.test/v1/"),
                "rich-model",
                null,
                []);

            var observed = created.AddMinutes(5);
            var staleAfter = observed.AddHours(1);

            var model = new ProviderModelMetadata(
                "rich-model",
                "example-provider",
                created,
                ProviderAvailabilityStatus.Available,
                ProviderHealthStatus.Healthy,
                [
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.TextGeneration,
                        CapabilityState.Supported),
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.Vision,
                        CapabilityState.Supported),
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.ToolCalling,
                        CapabilityState.Supported),
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.StructuredOutput,
                        CapabilityState.Supported),
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.Reasoning,
                        CapabilityState.Supported),
                    new CapabilityStateEntry(
                        HiveCapabilityKeys.Thinking,
                        CapabilityState.Supported)
                ],
                ["text", "image", "audio"],
                ["text"],
                family: "example-family",
                modelType: "chat",
                category: "multimodal",
                version: "1.0",
                operationalState: "active",
                thinkingOptions: ["low", "medium", "high"],
                defaultThinkingLevel: "medium",
                limits: new ProviderModelLimits(
                    contextWindowTokens: 131072,
                    maxInputTokens: 120000,
                    maxOutputTokens: 8192,
                    additionalConstraints: new Dictionary<string, JsonElement>
                    {
                        ["max_images_per_request"] = JsonSerializer.SerializeToElement(16)
                    }),
                pricing: new ProviderModelPricing(
                    [
                        new ProviderModelPrice(
                            "input_token",
                            1.25m,
                            "USD",
                            1_000_000m),
                        new ProviderModelPrice(
                            "output_token",
                            5m,
                            "USD",
                            1_000_000m)
                    ]),
                extensionData: new Dictionary<string, JsonElement>
                {
                    ["vendor_library"] =
                        JsonSerializer.SerializeToElement("deterministic-fixture"),
                    ["family_evidence"] =
                        JsonSerializer.SerializeToElement("example-family")
                },
                observedAtUtc: observed,
                staleAfterUtc: staleAfter);

            Snapshot = new ProviderDiscoverySnapshot(
                Provider.Id,
                Account.Id,
                Target.Endpoint,
                new ProviderOperationalMetadata(
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Healthy,
                    observed,
                    staleAfter,
                    rateLimitRemaining: 17),
                ProviderDiscoveryState.Supported,
                [model]);

            Management = DispatchProxy.Create<
                IHiveManagementFacade,
                ModelInformationManagementProxy>();
            ((ModelInformationManagementProxy)(object)Management).Configure(
                Provider,
                Account,
                Target,
                Snapshot,
                Context);
        }

        public ResourceAccessContext Context { get; }

        public Provider Provider { get; }

        public ProviderAccount Account { get; }

        public ExecutionTarget Target { get; }

        public ProviderDiscoverySnapshot Snapshot { get; }

        public IHiveManagementFacade Management { get; }
    }

    private class ModelInformationManagementProxy : DispatchProxy
    {
        private Provider? _provider;
        private ProviderAccount? _account;
        private ExecutionTarget? _target;
        private ProviderDiscoverySnapshot? _snapshot;
        private ResourceAccessContext? _context;

        public void Configure(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            ProviderDiscoverySnapshot snapshot,
            ResourceAccessContext context)
        {
            _provider = provider;
            _account = account;
            _target = target;
            _snapshot = snapshot;
            _context = context;
        }

        protected override object? Invoke(
            System.Reflection.MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("The fixture received an empty management method.");

            switch (targetMethod.Name)
            {
                case nameof(IHiveManagementFacade.ListProvidersAsync):
                    return Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(
                            [_provider!]));

                case nameof(IHiveManagementFacade.ListProviderAccountsAsync):
                    return Task.FromResult(
                        Result<IReadOnlyList<ProviderAccount>>.Success(
                            [_account!]));

                case nameof(IHiveManagementFacade.ListExecutionTargetsAsync):
                    return Task.FromResult(
                        Result<IReadOnlyList<ExecutionTarget>>.Success(
                            [_target!]));

                case nameof(IHiveManagementFacade.GetProviderDiscoveryAsync)
                    when args is { Length: 6 } &&
                         args[0] is ProviderId:
                    return Task.FromResult(
                        Result<ProviderDiscoverySnapshot>.Success(
                            _snapshot!));

                default:
                    throw new NotSupportedException(
                        $"The deterministic Model Information fixture does not implement '{targetMethod.Name}'.");
            }
        }
    }
}
