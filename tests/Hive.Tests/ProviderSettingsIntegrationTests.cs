using System.Collections.Concurrent;
using System.Reflection;
using System.Windows.Forms;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using Hive.Persistence;
using Hive.Tests.TestInfrastructure;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderSettingsIntegrationTests
{
    [Fact]
    public async Task ConfigureBuiltInProvider_RejectsUnknownAndAdvancedOnlyCatalogEntries()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        using var credential = SecretMaterial.Create("catalog-secret");

        var database = CreateDatabase("Hive_Test_ProviderSettingsCatalog");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var providerStore = new SqlProviderResourceStore(database.Options);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options));

        var unknown = await facade.ConfigureBuiltInProviderAsync(
            "does-not-exist",
            credential,
            context);
        Assert.True(unknown.IsFailure);
        Assert.Equal(ErrorCategory.NotFound, unknown.Error!.Category);

        using var cloudflareCredential = SecretMaterial.Create("cloudflare-secret");
        var advanced = await facade.ConfigureBuiltInProviderAsync(
            "cloudflare",
            cloudflareCredential,
            context);
        Assert.True(advanced.IsFailure);
        Assert.Equal(ErrorCategory.Unsupported, advanced.Error!.Category);
    }

    [Fact]
    public async Task ConfigureBuiltInProvider_CreatesDefaultAccountAndAutomaticTargets()
    {
        var database = CreateDatabase("Hive_Test_ProviderSettingsOnboarding");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var (context, providerStore, secretStore) = CreateInfrastructure(database);
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var discovery = new SettingsDiscovery(
            ModelSet("model-a", "model-b"),
            clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        using var credential = SecretMaterial.Create("onboarding-secret");

        var configured = await facade.ConfigureBuiltInProviderAsync(
            "openai",
            credential,
            context);

        Assert.True(configured.IsSuccess, configured.Error?.Message);
        Assert.Equal(2, configured.Value!.AutomaticTargetsCreated);
        Assert.Equal(2, configured.Value.ActiveAutomaticTargetCount);
        Assert.Equal(1, discovery.CallCount);
        Assert.Equal(["onboarding-secret"], discovery.Credentials);

        var providers = await facade.ListProvidersAsync(
            context,
            includeRetired: false);
        Assert.True(providers.IsSuccess, providers.Error?.Message);
        var provider = Assert.Single(providers.Value!);
        Assert.Equal("openai", provider.Key);
        Assert.Equal("openai-compatible", provider.TransportKind);

        var accounts = await facade.ListProviderAccountsAsync(
            provider.Id,
            context,
            includeRetired: false);
        Assert.True(accounts.IsSuccess, accounts.Error?.Message);
        var account = Assert.Single(accounts.Value!);
        Assert.Equal("default", account.Key);
        Assert.NotNull(account.CredentialSecret);

        var targets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(targets.IsSuccess, targets.Error?.Message);
        Assert.Equal(2, targets.Value!.Count);
        Assert.All(
            targets.Value!,
            target =>
            {
                Assert.Equal(ExecutionTargetManagementMode.Automatic, target.ManagementMode);
                Assert.Equal("https://api.openai.com/v1", target.Endpoint.AbsoluteUri);
            });
        Assert.DoesNotContain(
            targets.Value!,
            target => target.Model == "discovery-probe");
    }

    [Fact]
    public async Task ReplaceBuiltInProviderCredential_RefreshesUsingNewCredentialAndPreservesTargetIdentity()
    {
        var database = CreateDatabase("Hive_Test_ProviderSettingsCredentialReplacement");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var (context, providerStore, secretStore) = CreateInfrastructure(database);
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var discovery = new SettingsDiscovery(
            ModelSet("model-a"),
            clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        using var firstCredential = SecretMaterial.Create("first-secret");
        var configured = await facade.ConfigureBuiltInProviderAsync(
            "openai",
            firstCredential,
            context);
        Assert.True(configured.IsSuccess, configured.Error?.Message);

        var accountResult = await facade.ListProviderAccountsAsync(
            configured.Value!.Provider.Id,
            context,
            includeRetired: false);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);
        var account = Assert.Single(accountResult.Value!);

        var firstTargets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(firstTargets.IsSuccess, firstTargets.Error?.Message);
        var firstTarget = Assert.Single(firstTargets.Value!);
        var firstReference = account.CredentialSecret!.Value;

        using var secondCredential = SecretMaterial.Create("second-secret");
        var replaced = await facade.ReplaceBuiltInProviderCredentialAsync(
            configured.Value.Provider.Id,
            secondCredential,
            context);

        Assert.True(replaced.IsSuccess, replaced.Error?.Message);
        Assert.Equal(2, discovery.CallCount);
        Assert.Equal(
            ["first-secret", "second-secret"],
            discovery.Credentials);

        var accountAfterResult = await facade.ListProviderAccountsAsync(
            configured.Value.Provider.Id,
            context,
            includeRetired: false);
        Assert.True(accountAfterResult.IsSuccess, accountAfterResult.Error?.Message);
        var accountAfter = Assert.Single(accountAfterResult.Value!);
        Assert.Equal(firstReference, accountAfter.CredentialSecret!.Value);

        var secondTargets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(secondTargets.IsSuccess, secondTargets.Error?.Message);
        var secondTarget = Assert.Single(secondTargets.Value!);
        Assert.Equal(firstTarget.Id, secondTarget.Id);
        Assert.Equal(
            ExecutionTargetManagementMode.Automatic,
            secondTarget.ManagementMode);
    }

    [Fact]
    public async Task RefreshProvider_ReconcilesModelChurn_AndPreservesManualTargets()
    {
        var database = CreateDatabase("Hive_Test_ProviderSettingsReconciliation");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var (context, providerStore, secretStore) = CreateInfrastructure(database);
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var discovery = new SettingsDiscovery(
            ModelSet("model-a", "model-b"),
            clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        using var credential = SecretMaterial.Create("reconciliation-secret");

        var configured = await facade.ConfigureBuiltInProviderAsync(
            "openai",
            credential,
            context);
        Assert.True(configured.IsSuccess, configured.Error?.Message);

        discovery.EnqueueModels(ModelSet(new[] { "model-a", "model-c" }, "vision"));
        discovery.EnqueueModels(ModelSet("model-a", "model-b"));

        var provider = configured.Value!.Provider;
        var accounts = await facade.ListProviderAccountsAsync(
            provider.Id,
            context,
            includeRetired: false);
        Assert.True(accounts.IsSuccess, accounts.Error?.Message);
        var account = Assert.Single(accounts.Value!);

        var firstTargets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(firstTargets.IsSuccess, firstTargets.Error?.Message);

        var automaticA = Assert.Single(firstTargets.Value!, target => target.Model == "model-a");
        var automaticB = Assert.Single(firstTargets.Value!, target => target.Model == "model-b");

        var manual = new ExecutionTarget(
            CreateEnvelope(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context,
                clock),
            provider.Id,
            account.Id,
            "manual-model-a",
            "Manual Model A",
            automaticA.Endpoint,
            "model-a",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unsupported)
            ]);

        var manualCreated = await facade.CreateExecutionTargetAsync(
            manual,
            context);
        Assert.True(manualCreated.IsSuccess, manualCreated.Error?.Message);

        var refreshed = await facade.RefreshProviderAsync(
            provider.Id,
            context);
        Assert.True(refreshed.IsSuccess, refreshed.Error?.Message);
        Assert.Equal(1, refreshed.Value!.AutomaticTargetsCreated);
        Assert.Equal(1, refreshed.Value.AutomaticTargetsRetired);

        var secondTargets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(secondTargets.IsSuccess, secondTargets.Error?.Message);

        var currentAutomaticA = Assert.Single(
            secondTargets.Value!,
            target =>
                target.ManagementMode == ExecutionTargetManagementMode.Automatic &&
                target.Model == "model-a");
        Assert.Equal(automaticA.Id, currentAutomaticA.Id);
        Assert.Contains(
            currentAutomaticA.Capabilities,
            capability =>
                capability.Capability.Value == "vision" &&
                capability.State == CapabilityState.Supported);

        var retiredB = Assert.Single(secondTargets.Value!, target => target.Id == automaticB.Id);
        Assert.Equal(ResourceLifecycleStatus.Retired, retiredB.Resource.Lifecycle.Status);

        var automaticC = Assert.Single(
            secondTargets.Value!,
            target =>
                target.ManagementMode == ExecutionTargetManagementMode.Automatic &&
                target.Model == "model-c");
        Assert.Equal(
            ResourceLifecycleStatus.Active,
            automaticC.Resource.Lifecycle.Status);

        var unchangedManual = Assert.Single(secondTargets.Value!, target => target.Id == manual.Id);
        Assert.Equal(ExecutionTargetManagementMode.Manual, unchangedManual.ManagementMode);
        Assert.Equal(ResourceLifecycleStatus.Active, unchangedManual.Resource.Lifecycle.Status);
        Assert.Equal("Manual Model A", unchangedManual.DisplayName);
        Assert.Contains(
            unchangedManual.Capabilities,
            capability =>
                capability.Capability.Value == "vision" &&
                capability.State == CapabilityState.Unsupported);

        var returned = await facade.RefreshProviderAsync(
            provider.Id,
            context);
        Assert.True(returned.IsSuccess, returned.Error?.Message);
        Assert.Equal(1, returned.Value!.AutomaticTargetsReactivated);

        var thirdTargets = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(thirdTargets.IsSuccess, thirdTargets.Error?.Message);

        var reactivatedB = Assert.Single(thirdTargets.Value!, target => target.Id == automaticB.Id);
        Assert.Equal(ResourceLifecycleStatus.Active, reactivatedB.Resource.Lifecycle.Status);

        var retiredC = Assert.Single(thirdTargets.Value!, target => target.Model == "model-c");
        Assert.Equal(ResourceLifecycleStatus.Retired, retiredC.Resource.Lifecycle.Status);
    }

    [Fact]
    public async Task FailedProviderRefresh_PreservesExistingAutomaticTargets()
    {
        var database = CreateDatabase("Hive_Test_ProviderSettingsFailurePreservation");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var (context, providerStore, secretStore) = CreateInfrastructure(database);
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var discovery = new SettingsDiscovery(
            ModelSet("model-a"),
            clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        using var credential = SecretMaterial.Create("failure-secret");

        var configured = await facade.ConfigureBuiltInProviderAsync(
            "openai",
            credential,
            context);
        Assert.True(configured.IsSuccess, configured.Error?.Message);

        discovery.EnqueueResult(
            Result<ProviderDiscoverySnapshot>.Failure(
                new Error(
                    "hive.provider.discovery.test-failure",
                    ErrorCategory.External,
                    "The test discovery provider failed.")));

        var accountResult = await facade.ListProviderAccountsAsync(
            configured.Value!.Provider.Id,
            context,
            includeRetired: false);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);
        var account = Assert.Single(accountResult.Value!);

        var beforeResult = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(beforeResult.IsSuccess, beforeResult.Error?.Message);
        var before = Assert.Single(beforeResult.Value!);

        var refreshed = await facade.RefreshProviderAsync(
            configured.Value.Provider.Id,
            context);

        Assert.True(refreshed.IsSuccess, refreshed.Error?.Message);
        Assert.Single(refreshed.Value!.DiscoveryErrors);

        var afterResult = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(afterResult.IsSuccess, afterResult.Error?.Message);
        var after = Assert.Single(afterResult.Value!);

        Assert.Equal(before.Id, after.Id);
        Assert.Equal(ResourceLifecycleStatus.Active, after.Resource.Lifecycle.Status);
        Assert.Equal(before.Resource.Version, after.Resource.Version);
    }

    [Fact]
    public async Task ConcurrentProviderRefreshes_AreIdempotentForAutomaticTargets()
    {
        var database = CreateDatabase("Hive_Test_ProviderSettingsConcurrency");
        database.Reset();

        var migration = await new HiveDatabaseMigrator(database.Options).MigrateAsync();
        Assert.True(migration.IsSuccess, migration.Error?.Message);

        var (context, providerStore, secretStore) = CreateInfrastructure(database);
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var discovery = new SettingsDiscovery(
            ModelSet("model-a"),
            clock);

        using var facade = new HiveManagementFacade(
            providerStore,
            new SqlAgentDefinitionResourceStore(database.Options),
            new SqlWorkItemResourceStore(database.Options),
            secretStore,
            providerCapabilityDiscovery: discovery,
            clock: clock);

        using var credential = SecretMaterial.Create("concurrency-secret");

        var configured = await facade.ConfigureBuiltInProviderAsync(
            "openai",
            credential,
            context);
        Assert.True(configured.IsSuccess, configured.Error?.Message);

        var requests = Enumerable
            .Range(0, 4)
            .Select(_ => facade.RefreshProviderAsync(
                configured.Value!.Provider.Id,
                context))
            .ToArray();

        var results = await Task.WhenAll(requests);

        Assert.All(
            results,
            result => Assert.True(result.IsSuccess, result.Error?.Message));

        var accountResult = await facade.ListProviderAccountsAsync(
            configured.Value!.Provider.Id,
            context,
            includeRetired: false);
        Assert.True(accountResult.IsSuccess, accountResult.Error?.Message);
        var account = Assert.Single(accountResult.Value!);

        var targetResult = await facade.ListExecutionTargetsAsync(
            account.Id,
            context,
            includeRetired: true);
        Assert.True(targetResult.IsSuccess, targetResult.Error?.Message);

        var automatic = Assert.Single(
            targetResult.Value!,
            target =>
                target.ManagementMode == ExecutionTargetManagementMode.Automatic &&
                target.Model == "model-a");

        Assert.Equal(ResourceLifecycleStatus.Active, automatic.Resource.Lifecycle.Status);
        Assert.Equal(5, discovery.CallCount);
    }

    [Fact]
    public void ExecutionTargetEditor_AppliesSelectedManagementModeToNewTarget()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        var provider = new Provider(
            CreateEnvelope(
                ResourceKind.Provider,
                ProviderId.New(),
                context,
                new FakeClock(now)),
            "revision-provider",
            "Revision Provider",
            "openai-compatible");

        var account = new ProviderAccount(
            CreateEnvelope(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context,
                new FakeClock(now)),
            provider.Id,
            "default",
            "Default Account");

        var (management, _) = HiveWorkspaceLifecycleTests.ManagementFacadeProxy.Create();
        using var editor = new HiveExecutionTargetEditorForm(
            target: null,
            provider,
            account,
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        editor.ManagementModeSelector.SelectedItem =
            ExecutionTargetManagementMode.Automatic;

        var keyTextBox = FindControl<TextBox>(
            editor,
            box => string.Equals(
                box.PlaceholderText,
                "e.g. llama-production",
                StringComparison.Ordinal));
        var nameTextBox = FindControl<TextBox>(
            editor,
            box => string.Equals(
                box.PlaceholderText,
                "e.g. Production Llama",
                StringComparison.Ordinal));
        var endpointTextBox = FindControl<TextBox>(
            editor,
            box => string.Equals(
                box.PlaceholderText,
                "https://api.example.com/v1",
                StringComparison.Ordinal));
        var modelSelector = editor.ModelSelector;

        Assert.NotNull(keyTextBox);
        Assert.NotNull(nameTextBox);
        Assert.NotNull(endpointTextBox);
        Assert.Equal(ComboBoxStyle.DropDown, modelSelector.DropDownStyle);

        keyTextBox!.Text = "automatic-target";
        nameTextBox!.Text = "Automatic Target";
        endpointTextBox!.Text = "https://example.test/v1";
        modelSelector.Text = "model-a";

        Assert.NotNull(editor.AcceptButton);
        ((IButtonControl)editor.AcceptButton!).PerformClick();

        Assert.NotNull(editor.Definition);
        Assert.Equal(
            ExecutionTargetManagementMode.Automatic,
            editor.Definition!.ManagementMode);
    }

    [Fact]
    public void ProviderSetupEditor_UsesActualLineBreaksInProviderDetails()
    {
        using var editor = new HiveProviderSetupEditorForm(
            existing: null,
            new HiveThemeManager(HiveThemeMode.Light));

        var details = FindControl<Label>(editor, label =>
            label.Text.StartsWith("Endpoint:", StringComparison.Ordinal));

        Assert.NotNull(details);
        Assert.Contains(Environment.NewLine, details!.Text);
        Assert.DoesNotContain(@"\r\n", details.Text);
    }

    [Fact]
    public async Task ProvidersSettings_OptionalCredentialProviderShowsOptionalStatusWhenNotConfigured()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var clock = new FakeClock(
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero));

        var provider = new Provider(
            CreateEnvelope(
                ResourceKind.Provider,
                ProviderId.New(),
                context,
                clock),
            "ollama",
            "Ollama",
            "openai-compatible");

        var account = new ProviderAccount(
            CreateEnvelope(
                ResourceKind.ProviderAccount,
                ProviderAccountId.New(),
                context,
                clock),
            provider.Id,
            "default",
            "Default Account");

        var target = new ExecutionTarget(
            CreateEnvelope(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context,
                clock),
            provider.Id,
            account.Id,
            "auto-local-model",
            "local-model",
            new Uri("http://localhost:11434/v1"),
            "local-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]).WithManagementMode(ExecutionTargetManagementMode.Automatic);

        var (management, _) = ProvidersSettingsManagementProxy.Create(
            provider,
            account,
            target);

        using var view = new HiveProvidersSettingsView(
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        await view.InitializeAsync();

        var list = FindControl<ListView>(view);

        Assert.NotNull(list);
        var row = Assert.Single(list!.Items.Cast<ListViewItem>());
        Assert.Equal("Optional — not configured", row.SubItems[2].Text);
    }

    [Fact]
    public void ExecutionTargetManagementModeDefaultsToManual()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());

        var target = new ExecutionTarget(
            CreateEnvelope(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context),
            ProviderId.New(),
            ProviderAccountId.New(),
            "manual-target",
            "Manual Target",
            new Uri("https://example.test/v1"),
            "model-a",
            null,
            Array.Empty<CapabilityStateEntry>());

        Assert.Equal(ExecutionTargetManagementMode.Manual, target.ManagementMode);
    }

    private static TControl? FindControl<TControl>(Control root)
        where TControl : Control =>
        FindControl<TControl>(root, static _ => true);

    private static TControl? FindControl<TControl>(
        Control root,
        Func<TControl, bool> predicate)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl match && predicate(match))
                return match;

            var nested = FindControl(child, predicate);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private static PersistenceTestDatabase CreateDatabase(string name) =>
        new(name);

    private static (
        ResourceAccessContext Context,
        SqlProviderResourceStore ProviderStore,
        SqlDpapiSecretStore SecretStore)
        CreateInfrastructure(PersistenceTestDatabase database)
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        return (
            context,
            new SqlProviderResourceStore(database.Options),
            new SqlDpapiSecretStore(database.Options));
    }

    private static ResourceEnvelope<TIdentity> CreateEnvelope<TIdentity>(
        ResourceKind kind,
        TIdentity identity,
        ResourceAccessContext context,
        IClock? clock = null)
        where TIdentity : struct
    {
        var now = (clock ?? new FakeClock(DateTimeOffset.UtcNow)).UtcNow;

        return new ResourceEnvelope<TIdentity>(
            kind,
            identity,
            context.PrincipalId!.Value,
            ResourceScope.Tenant(context.TenantId!.Value),
            ResourceVersion.Initial,
            new ResourceProvenance(
                context.PrincipalId.Value,
                now,
                CorrelationId.New()),
            ResourceLifecycle.Active(now));
    }

    private static IReadOnlyList<ProviderModelMetadata> ModelSet(
        params string[] modelIds) =>
        ModelSet(modelIds, null);

    private static IReadOnlyList<ProviderModelMetadata> ModelSet(
        string[] modelIds,
        string? capability) =>
        modelIds
            .Select(
                model => new ProviderModelMetadata(
                    model,
                    "test",
                    null,
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Healthy,
                    capability is null
                        ? Array.Empty<CapabilityStateEntry>()
                        : [
                            new CapabilityStateEntry(
                                new CapabilityKey(capability),
                                CapabilityState.Supported)
                        ]))
            .ToArray();

    private class ProvidersSettingsManagementProxy : DispatchProxy
    {
        private Provider _provider = null!;
        private ProviderAccount _account = null!;
        private ExecutionTarget _target = null!;

        public static (
            IHiveManagementFacade Management,
            ProvidersSettingsManagementProxy Proxy) Create(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target)
        {
            var management =
                (IHiveManagementFacade)Create<
                    IHiveManagementFacade,
                    ProvidersSettingsManagementProxy>();

            var proxy = (ProvidersSettingsManagementProxy)(object)management;
            proxy._provider = provider;
            proxy._account = account;
            proxy._target = target;

            return (management, proxy);
        }

        protected override object Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IHiveManagementFacade.ListProvidersAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(
                            [_provider])),

                nameof(IHiveManagementFacade.ListProviderAccountsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ProviderAccount>>.Success(
                            [_account])),

                nameof(IHiveManagementFacade.ListExecutionTargetsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ExecutionTarget>>.Success(
                            [_target])),

                _ => throw new NotSupportedException(
                    $"The provider settings test proxy does not implement '{targetMethod?.Name}'.")
            };
        }
    }

    private sealed class SettingsDiscovery : IProviderCapabilityDiscovery
    {
        private readonly ConcurrentQueue<Result<ProviderDiscoverySnapshot>> _results;
        private readonly IReadOnlyList<ProviderModelMetadata> _fallbackModels;
        private int _callCount;
        private readonly List<string?> _credentials = new();

        public SettingsDiscovery(
            IReadOnlyList<ProviderModelMetadata> initialModels,
            IClock clock)
        {
            _results = new ConcurrentQueue<Result<ProviderDiscoverySnapshot>>();
            _fallbackModels = initialModels;
            Clock = clock;
        }

        public IClock Clock { get; }

        public int CallCount => Volatile.Read(ref _callCount);

        public IReadOnlyList<string?> Credentials
        {
            get
            {
                lock (_credentials)
                {
                    return _credentials.ToArray();
                }
            }
        }

        public void EnqueueModels(IReadOnlyList<ProviderModelMetadata> models)
        {
            _results.Enqueue(
                Result<ProviderDiscoverySnapshot>.Success(
                    new ProviderDiscoverySnapshot(
                        ProviderId.New(),
                        ProviderAccountId.New(),
                        new Uri("https://example.test/v1"),
                        new ProviderOperationalMetadata(
                            ProviderAvailabilityStatus.Available,
                            ProviderHealthStatus.Healthy,
                            Clock.UtcNow,
                            Clock.UtcNow.AddMinutes(5)),
                        ProviderDiscoveryState.Supported,
                        models)));
        }

        public void EnqueueResult(Result<ProviderDiscoverySnapshot> result) =>
            _results.Enqueue(result);

        public Task<Result<ProviderDiscoverySnapshot>> DiscoverAsync(
            Provider provider,
            ProviderAccount account,
            ExecutionTarget target,
            SecretMaterial? credential,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            lock (_credentials)
            {
                _credentials.Add(credential?.Reveal());
            }

            Interlocked.Increment(ref _callCount);

            if (_results.TryDequeue(out var queued))
            {
                return Task.FromResult(
                    queued.IsFailure
                        ? queued
                        : Result<ProviderDiscoverySnapshot>.Success(
                            RebuildSnapshot(
                                queued.Value!,
                                provider,
                                account,
                                target)));
            }

            return Task.FromResult(
                Result<ProviderDiscoverySnapshot>.Success(
                    new ProviderDiscoverySnapshot(
                        provider.Id,
                        account.Id,
                        target.Endpoint,
                        new ProviderOperationalMetadata(
                            ProviderAvailabilityStatus.Available,
                            ProviderHealthStatus.Healthy,
                            Clock.UtcNow,
                            Clock.UtcNow.AddMinutes(5)),
                        ProviderDiscoveryState.Supported,
                        _fallbackModels)));

            static ProviderDiscoverySnapshot RebuildSnapshot(
                ProviderDiscoverySnapshot snapshot,
                Provider provider,
                ProviderAccount account,
                ExecutionTarget target) =>
                new(
                    provider.Id,
                    account.Id,
                    target.Endpoint,
                    snapshot.Operational,
                    snapshot.ModelEnumerationState,
                    snapshot.Models);
        }
    }
}
