using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using System.Reflection;
using Xunit;

namespace Hive.Tests;

public sealed class HiveExecutionTargetDiscoverySettingsTests
{
    [WinFormsFact]
    public async Task DiscoveryPanel_UsesManagementAndPresentsDiscoveredModelsAndMetadata()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, proxy) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            target.Model,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();

        Assert.Equal(1, proxy.DiscoveryInvocationCount);
        Assert.Single(panel.Models);
        Assert.Equal("vision-model", panel.ModelSelector.Items[0]!.ToString());
        Assert.Contains("Discovered 1 model(s).", panel.StatusLabel.Text);
        Assert.Contains("Available", panel.MetadataLabel.Text);
        Assert.Contains("Healthy", panel.MetadataLabel.Text);
    }

    [WinFormsFact]
    public async Task DiscoveryPanel_SelectionRaisesModelWithoutChangingConfiguredCapabilityOverrides()
    {
        var target = CreateTarget(
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unsupported)
            ]);
        var snapshot = CreateSnapshot(
            target,
            capabilities:
            [
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Supported)
            ]);
        var (management, _) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            target.Model,
            CreateContext(),
            themeManager);

        string? selectedModel = null;
        panel.ModelSelected += (_, e) => selectedModel = e.Model.ModelId;

        await panel.InitializeAsync();

        panel.ModelSelector.SelectedIndex = 0;

        Assert.Equal("vision-model", selectedModel);
        Assert.Equal(
            CapabilityState.Unsupported,
            target.Capabilities.Single().State);
    }

    [WinFormsFact]
    public async Task Editor_AddPrefillsBuiltInProviderEndpointAndUsesEditableModelCombo()
    {
        var context = CreateContext();
        var providerId = ProviderId.New();
        var accountId = ProviderAccountId.New();
        var provider = CreateProvider(
            providerId,
            context,
            "openai",
            "OpenAI");
        var account = CreateAccount(accountId, providerId, context);
        var snapshot = new ProviderDiscoverySnapshot(
            providerId,
            accountId,
            new Uri("https://api.openai.com/v1"),
            CreateOperationalMetadata(),
            ProviderDiscoveryState.Supported,
            [
                new ProviderModelMetadata(
                    "vision-model",
                    "example",
                    null,
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Healthy,
                    [])
            ]);
        var (management, _) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var editor = new HiveExecutionTargetEditorForm(
            null,
            provider,
            account,
            management,
            context,
            themeManager);

        Assert.Equal(
            "https://api.openai.com/v1",
            editor.EndpointTextBox.Text);
        Assert.Equal(
            ComboBoxStyle.DropDown,
            editor.ModelSelector.DropDownStyle);

        await editor.DiscoveryPanel!.InitializeAsync();

        editor.ModelSelector.SelectedIndex = 0;
        Assert.Equal("vision-model", editor.ModelSelector.Text);

        editor.ModelSelector.Text = "custom-model";
        Assert.Equal("custom-model", editor.ModelSelector.Text);
    }

    [WinFormsFact]
    public async Task Editor_AutomaticTargetClearsStaleDiscoveredCapabilitiesWhenModelBecomesCustom()
    {
        var target = CreateTarget(
            [
                new CapabilityStateEntry(
                    HiveCapabilityKeys.Vision,
                    CapabilityState.Supported)
            ])
            .WithManagementMode(ExecutionTargetManagementMode.Automatic);
        var snapshot = CreateSnapshot(target);
        var (management, _) = DiscoveryManagementProxy.Create(snapshot);
        var context = CreateContext();
        var provider = CreateProvider(target.ProviderId, context);
        var account = CreateAccount(
            target.ProviderAccountId,
            target.ProviderId,
            context);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var editor = new HiveExecutionTargetEditorForm(
            target,
            provider,
            account,
            management,
            context,
            themeManager);

        await editor.DiscoveryPanel!.InitializeAsync();

        editor.ModelSelector.SelectedIndex = 0;

        var automaticCapabilities =
            editor.CapabilityEditor.GetConfiguredCapabilities();

        Assert.Equal(2, automaticCapabilities.Count);
        Assert.Contains(
            automaticCapabilities,
            item => item.Capability == HiveCapabilityKeys.Vision &&
                    item.State == CapabilityState.Supported);
        Assert.Contains(
            automaticCapabilities,
            item => item.Capability == HiveCapabilityKeys.ToolCalling &&
                    item.State == CapabilityState.Supported);

        editor.ModelSelector.Text = "custom-model";

        Assert.Empty(editor.CapabilityEditor.GetConfiguredCapabilities());
    }

    [WinFormsFact]
    public void Editor_ConnectionTestStatusLivesInFooterActionBar()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, _) = DiscoveryManagementProxy.Create(snapshot);
        var context = CreateContext();
        var provider = CreateProvider(target.ProviderId, context);
        var account = CreateAccount(
            target.ProviderAccountId,
            target.ProviderId,
            context);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var editor = new HiveExecutionTargetEditorForm(
            target,
            provider,
            account,
            management,
            context,
            themeManager);

        var editorLayout = FindControl<HiveEditorLayout>(editor);

        Assert.NotNull(editorLayout);

        var footer = editorLayout!.FooterPanel;

        Assert.Contains(
            editor.TestStatusLabel,
            footer.Controls.Cast<Control>()
                .SelectMany(control =>
                    control is Panel panel
                        ? panel.Controls.Cast<Control>()
                        : Enumerable.Empty<Control>()));
        Assert.Contains(editor.TestButton, footer.Controls.Cast<Control>());
    }

    [WinFormsFact]
    public async Task ExecutionTargetsView_SelectingProviderSelectsDefaultAccount()
    {
        var context = CreateContext();
        var provider = CreateProvider(
            ProviderId.New(),
            context);
        var secondary = CreateAccount(
            ProviderAccountId.New(),
            provider.Id,
            context,
            key: "secondary",
            displayName: "Secondary Account");
        var @default = CreateAccount(
            ProviderAccountId.New(),
            provider.Id,
            context,
            key: "default",
            displayName: "Default Account");

        var management = ExecutionTargetsManagementProxy.Create(
            [provider],
            [secondary, @default]);

        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var view = new HiveExecutionTargetsSettingsView(
            management,
            context,
            themeManager);

        await view.InitializeAsync();

        view.ProviderSelector.SelectedIndex = 0;

        Assert.Equal("Default Account", view.AccountSelector.SelectedItem?.ToString());
    }

    [WinFormsFact]
    public async Task Editor_AutomaticallyStartsDiscoveryWhenOpenedForPersistedTarget()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, proxy) = DiscoveryManagementProxy.Create(snapshot);
        var context = CreateContext();
        var provider = CreateProvider(target.ProviderId, context);
        var account = CreateAccount(
            target.ProviderAccountId,
            target.ProviderId,
            context);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var editor = new HiveExecutionTargetEditorForm(
            target,
            provider,
            account,
            management,
            context,
            themeManager);

        editor.Show();

        try
        {
            await proxy.FirstStarted.Task;

            Assert.Equal(1, proxy.DiscoveryInvocationCount);
            Assert.NotNull(editor.DiscoveryPanel);
        }
        finally
        {
            editor.Close();
        }
    }

    [WinFormsFact]
    public async Task Editor_AppliesSelectedDiscoveredModelToEditableModelField()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, _) = DiscoveryManagementProxy.Create(snapshot);
        var context = CreateContext();
        var provider = CreateProvider(target.ProviderId, context);
        var account = CreateAccount(
            target.ProviderAccountId,
            target.ProviderId,
            context);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var editor = new HiveExecutionTargetEditorForm(
            target,
            provider,
            account,
            management,
            context,
            themeManager);

        Assert.NotNull(editor.DiscoveryPanel);
        var panel = editor.DiscoveryPanel!;

        await panel.InitializeAsync();

        panel.ModelSelector.SelectedIndex = 0;

        Assert.Equal("vision-model", editor.ModelSelector.Text);
    }

    [WinFormsFact]
    public async Task DiscoveryPanel_FailureLeavesManualEntryAvailable()
    {
        var target = CreateTarget();
        var (management, _) = DiscoveryManagementProxy.CreateFailure(
            new Error(
                "hive.tests.discovery-failed",
                ErrorCategory.External,
                "The provider discovery request failed safely."));
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            target.Model,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();

        Assert.Empty(panel.Models);
        Assert.Contains("Discovery failed (hive.tests.discovery-failed).", panel.StatusLabel.Text);
        Assert.Contains("Manual model entry remains available.", panel.StatusLabel.Text);
        Assert.True(panel.ModelSelector.Enabled);
    }

    [WinFormsFact]
    public async Task DiscoveryPanel_UnsupportedEnumerationLeavesManualEntryAvailable()
    {
        var target = CreateTarget();
        var snapshot = new ProviderDiscoverySnapshot(
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            CreateOperationalMetadata(),
            ProviderDiscoveryState.Unsupported,
            Array.Empty<ProviderModelMetadata>());
        var (management, _) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            target.Model,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();

        Assert.Empty(panel.Models);
        Assert.Contains("not supported", panel.StatusLabel.Text);
        Assert.True(panel.ModelSelector.Enabled);
    }

    [WinFormsFact]
    public async Task DiscoveryPanel_SupersededCancellationDoesNotApplyOlderResult()
    {
        var target = CreateTarget();
        var first = CreateSnapshot(target);
        var second = CreateSnapshot(
            target,
            models:
            [
                new ProviderModelMetadata(
                    "second-model",
                    "example",
                    null,
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Healthy,
                    [])
            ]);
        var (management, proxy) = DiscoveryManagementProxy.Create(first, second);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            target.Model,
            CreateContext(),
            themeManager);

        var firstTask = panel.InitializeAsync();
        await proxy.FirstStarted.Task;

        var secondTask = panel.RefreshAsync();
        proxy.SecondCompletion.TrySetResult(
            Result<ProviderDiscoverySnapshot>.Success(second));

        await secondTask;
        proxy.FirstCompletion.TrySetCanceled();

        await firstTask;

        Assert.Single(panel.Models);
        Assert.Equal("second-model", panel.ModelSelector.Items[0]!.ToString());
        Assert.Contains("Discovered 1 model(s).", panel.StatusLabel.Text);
    }

    [WinFormsFact]
    public async Task DiscoveryPanel_EndpointChangePreventsRefreshUntilSaved()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, proxy) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            target.Model,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();
        panel.MarkEndpointConfigurationChanged();
        await panel.RefreshAsync();

        Assert.Equal(1, proxy.DiscoveryInvocationCount);
        Assert.Contains("Save the target", panel.StatusLabel.Text);
        Assert.Empty(panel.Models);
    }

    private static ProviderDiscoverySnapshot CreateSnapshot(
        ExecutionTarget target,
        IReadOnlyList<CapabilityStateEntry>? capabilities = null,
        IReadOnlyList<ProviderModelMetadata>? models = null) =>
        new(
            target.ProviderId,
            target.ProviderAccountId,
            target.Endpoint,
            CreateOperationalMetadata(),
            ProviderDiscoveryState.Supported,
            models ??
            [
                new ProviderModelMetadata(
                    "vision-model",
                    "example",
                    null,
                    ProviderAvailabilityStatus.Available,
                    ProviderHealthStatus.Healthy,
                    capabilities ??
                    [
                        new CapabilityStateEntry(
                            new CapabilityKey("vision"),
                            CapabilityState.Supported),
                        new CapabilityStateEntry(
                            new CapabilityKey("tool.calling"),
                            CapabilityState.Supported)
                    ])
            ]);

    private static ProviderOperationalMetadata CreateOperationalMetadata() =>
        new(
            ProviderAvailabilityStatus.Available,
            ProviderHealthStatus.Healthy,
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(29),
            rateLimitRemaining: 120);

    private static ExecutionTarget CreateTarget(
        IReadOnlyList<CapabilityStateEntry>? capabilities = null)
    {
        var context = CreateContext();
        var now = DateTimeOffset.UtcNow;
        return new ExecutionTarget(
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
            ProviderId.New(),
            ProviderAccountId.New(),
            "settings-discovery-target",
            "Settings Discovery Target",
            new Uri("https://example.test/v1"),
            "manual-model",
            null,
            capabilities ?? Array.Empty<CapabilityStateEntry>());
    }

    private static ResourceAccessContext CreateContext() =>
        new(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());


    private static Provider CreateProvider(
        ProviderId providerId,
        ResourceAccessContext context,
        string key = "settings-discovery-provider",
        string? displayName = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                providerId,
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            key,
            displayName ?? "Settings Discovery Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateAccount(
        ProviderAccountId accountId,
        ProviderId providerId,
        ResourceAccessContext context,
        string key = "settings-discovery-account",
        string displayName = "Settings Discovery Account")
    {
        var now = DateTimeOffset.UtcNow;
        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                accountId,
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            key,
            displayName);
    }

    private static TControl? FindControl<TControl>(Control root)
        where TControl : Control
    {
        foreach (Control child in root.Controls)
        {
            if (child is TControl match)
                return match;

            var nested = FindControl<TControl>(child);
            if (nested is not null)
                return nested;
        }

        return null;
    }

    private class ExecutionTargetsManagementProxy : DispatchProxy
    {
        private IReadOnlyList<Provider> _providers = [];
        private IReadOnlyList<ProviderAccount> _accounts = [];

        public static IHiveManagementFacade Create(
            IReadOnlyList<Provider> providers,
            IReadOnlyList<ProviderAccount> accounts)
        {
            ArgumentNullException.ThrowIfNull(providers);
            ArgumentNullException.ThrowIfNull(accounts);

            var management =
                (IHiveManagementFacade)DispatchProxy.Create<
                    IHiveManagementFacade,
                    ExecutionTargetsManagementProxy>();
            var proxy = (ExecutionTargetsManagementProxy)(object)management;
            proxy._providers = providers;
            proxy._accounts = accounts;
            return management;
        }

        protected override object Invoke(
            System.Reflection.MethodInfo? targetMethod,
            object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IHiveManagementFacade.ListProvidersAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(
                            _providers)),

                nameof(IHiveManagementFacade.ListProviderAccountsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ProviderAccount>>.Success(
                            _accounts)),

                nameof(IHiveManagementFacade.ListExecutionTargetsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ExecutionTarget>>.Success(
                            [])),

                _ => throw new NotSupportedException(
                    $"The Execution Targets UI test proxy does not implement '{targetMethod?.Name}'.")
            };
        }
    }

    private class DiscoveryManagementProxy : DispatchProxy
    {
        private readonly List<ProviderDiscoverySnapshot> _snapshots = new();
        private readonly TaskCompletionSource<bool> _firstStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Result<ProviderDiscoverySnapshot>> _firstCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<Result<ProviderDiscoverySnapshot>> _secondCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Error? _failure;
        private int _discoveryInvocationCount;

        public TaskCompletionSource<bool> FirstStarted => _firstStarted;

        public TaskCompletionSource<Result<ProviderDiscoverySnapshot>> FirstCompletion =>
            _firstCompletion;

        public TaskCompletionSource<Result<ProviderDiscoverySnapshot>> SecondCompletion =>
            _secondCompletion;

        public int DiscoveryInvocationCount =>
            Volatile.Read(ref _discoveryInvocationCount);

        public static (
            IHiveManagementFacade Management,
            DiscoveryManagementProxy Proxy) Create(
            params ProviderDiscoverySnapshot[] snapshots)
        {
            var management =
                (IHiveManagementFacade)Create<IHiveManagementFacade, DiscoveryManagementProxy>();
            var proxy = (DiscoveryManagementProxy)(object)management;
            proxy._snapshots.AddRange(snapshots);
            return (management, proxy);
        }

        public static (
            IHiveManagementFacade Management,
            DiscoveryManagementProxy Proxy) CreateFailure(
            Error error)
        {
            var management =
                (IHiveManagementFacade)Create<IHiveManagementFacade, DiscoveryManagementProxy>();
            var proxy = (DiscoveryManagementProxy)(object)management;
            proxy._failure = error ?? throw new ArgumentNullException(nameof(error));
            return (management, proxy);
        }

        protected override object Invoke(
            System.Reflection.MethodInfo? targetMethod,
            object?[]? args)
        {
            if (targetMethod?.Name == nameof(IHiveManagementFacade.GetProviderDiscoveryAsync))
            {
                var invocation = Interlocked.Increment(ref _discoveryInvocationCount);

                if (_failure is not null)
                {
                    return Task.FromResult(
                        Result<ProviderDiscoverySnapshot>.Failure(_failure));
                }
                _firstStarted.TrySetResult(true);

                if (_snapshots.Count == 1)
                    return Task.FromResult(
                        Result<ProviderDiscoverySnapshot>.Success(
                            _snapshots[0]));

                return invocation == 1
                    ? _firstCompletion.Task
                    : _secondCompletion.Task;
            }

            throw new NotSupportedException(
                $"The discovery Settings test proxy does not implement '{targetMethod?.Name}'.");
        }
    }
}
