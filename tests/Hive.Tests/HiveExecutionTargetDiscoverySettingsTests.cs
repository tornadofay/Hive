using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using System.Reflection;
using Xunit;

namespace Hive.Tests;

public sealed class HiveExecutionTargetDiscoverySettingsTests
{
    [Fact]
    public async Task DiscoveryPanel_UsesManagementAndPresentsDiscoveredModelsAndMetadata()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, proxy) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();

        Assert.Equal(1, proxy.DiscoveryInvocationCount);
        Assert.Single(panel.Models);
        Assert.Equal("vision-model", panel.ModelSelector.Items[0]!.ToString());
        Assert.Contains("Discovered 1 model(s).", panel.StatusLabel.Text);
        Assert.Contains("Available", panel.StatusLabel.Text);
        Assert.Contains("Healthy", panel.StatusLabel.Text);
    }

    [Fact]
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
            target,
            CreateContext(),
            themeManager);

        string? selectedModel = null;
        panel.ModelSelected += (_, e) => selectedModel = e.Model.ModelId;

        await panel.InitializeAsync();

        panel.ModelSelector.SelectedIndex = 0;
        panel.UseModelButton.PerformClick();

        Assert.Equal("vision-model", selectedModel);
        Assert.Equal(
            CapabilityState.Unsupported,
            target.Capabilities.Single().State);
    }

    [Fact]
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

    [Fact]
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
        panel.UseModelButton.PerformClick();

        Assert.Equal("vision-model", editor.ModelTextBox.Text);
    }

    [Fact]
    public async Task DiscoveryPanel_FailureLeavesManualEntryAvailable()
    {
        var target = CreateTarget();
        var (management, _) = DiscoveryManagementProxy.CreateFailure(
            new Error(
                "hive.tests.discovery-failed",
                ErrorCategory.Transport,
                "The provider discovery request failed safely."));
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();

        Assert.Empty(panel.Models);
        Assert.Contains("Discovery failed:", panel.StatusLabel.Text);
        Assert.Contains("Manual model entry remains available.", panel.StatusLabel.Text);
        Assert.False(panel.ModelSelector.Enabled);
    }

    [Fact]
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
            target,
            CreateContext(),
            themeManager);

        await panel.InitializeAsync();

        Assert.Empty(panel.Models);
        Assert.Contains("not supported", panel.StatusLabel.Text);
        Assert.False(panel.ModelSelector.Enabled);
    }

    [Fact]
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
            target,
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

    [Fact]
    public async Task DiscoveryPanel_EndpointChangePreventsRefreshUntilSaved()
    {
        var target = CreateTarget();
        var snapshot = CreateSnapshot(target);
        var (management, proxy) = DiscoveryManagementProxy.Create(snapshot);
        var themeManager = new HiveThemeManager(HiveThemeMode.Light);

        using var panel = new HiveProviderModelDiscoveryPanel(
            management,
            target,
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


    private static Provider CreateProvider(ProviderId providerId, ResourceAccessContext context)
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
            "settings-discovery-provider",
            "Settings Discovery Provider",
            "openai-compatible");
    }

    private static ProviderAccount CreateAccount(
        ProviderAccountId accountId,
        ProviderId providerId,
        ResourceAccessContext context)
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
            "settings-discovery-account",
            "Settings Discovery Account");
    }

    private sealed class DiscoveryManagementProxy : DispatchProxy
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
