using System.Reflection;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Management;
using Xunit;

namespace Hive.Tests;

public sealed class ExecutionTargetFavoriteSettingsUiTests
{
    [WinFormsFact]
    public void ProviderSettings_UsesProvidersAsFirstTabAndFavoritesAsSecondTab()
    {
        var management = UiManagementProxy.Create();
        var context = CreateContext();
        var theme = new HiveThemeManager(HiveThemeMode.Light);

        using var view = new HiveProvidersSettingsView(
            management,
            context,
            theme);

        Assert.Equal(2, view.NavigationTabs.TabPages.Count);
        Assert.Equal("Providers", view.NavigationTabs.TabPages[0].Text);
        Assert.Equal("Favorite Execution Targets", view.NavigationTabs.TabPages[1].Text);
        Assert.Equal(0, view.NavigationTabs.SelectedIndex);
    }

    [WinFormsFact]
    public async Task FavoriteSettings_LoadsProvidersAccountsTargetsAndStoredFavorites()
    {
        var context = CreateContext();
        var provider = CreateProvider(context, "Provider One");
        var account = CreateAccount(provider.Id, context, "Account One");
        var first = CreateTarget(provider.Id, account.Id, context, "first", "First Target");
        var second = CreateTarget(provider.Id, account.Id, context, "second", "Second Target");

        var management = UiManagementProxy.Create(
            [provider],
            [account],
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [account.Id] = [first, second]
            },
            [second.Id]);

        using var view = new HiveFavoriteExecutionTargetsSettingsView(
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        await view.InitializeAsync();

        Assert.Equal(2, view.ProviderSelector.Items.Count);
        Assert.Equal("All Providers", view.ProviderSelector.Items[0]!.ToString());
        Assert.Equal("Provider One", view.ProviderSelector.Items[1]!.ToString());
        Assert.Equal(2, view.AccountSelector.Items.Count);
        Assert.Equal("All Accounts", view.AccountSelector.Items[0]!.ToString());
        Assert.Equal("Account One", view.AccountSelector.Items[1]!.ToString());

        Assert.Equal(2, view.TargetList.Items.Count);
        var firstItem = view.TargetList.Items
            .Cast<ListViewItem>()
            .Single(item => item.Tag is ExecutionTargetId id && id == first.Id);
        var secondItem = view.TargetList.Items
            .Cast<ListViewItem>()
            .Single(item => item.Tag is ExecutionTargetId id && id == second.Id);

        Assert.False(firstItem.Checked);
        Assert.True(secondItem.Checked);
    }

    [WinFormsFact]
    public async Task FavoriteSettings_AccountFilterNarrowsVisibleTargets()
    {
        var context = CreateContext();
        var provider = CreateProvider(context, "Provider One");
        var firstAccount = CreateAccount(provider.Id, context, "Account One");
        var secondAccount = CreateAccount(provider.Id, context, "Account Two");
        var first = CreateTarget(provider.Id, firstAccount.Id, context, "first", "First Target");
        var second = CreateTarget(provider.Id, secondAccount.Id, context, "second", "Second Target");

        var proxy = UiManagementProxy.Create(
            [provider],
            [firstAccount, secondAccount],
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [firstAccount.Id] = [first],
                [secondAccount.Id] = [second]
            },
            [first.Id, second.Id]);

        using var view = new HiveFavoriteExecutionTargetsSettingsView(
            proxy,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        await view.InitializeAsync();

        view.AccountSelector.SelectedIndex = 2;
        await proxy.WaitForAccountLoadAsync(secondAccount.Id);

        Assert.Single(view.TargetList.Items);
        Assert.Equal(second.Id, ((ListViewItem)view.TargetList.Items[0]).Tag);
    }

    [WinFormsFact]
    public void AgentEditor_ShowsFavoritePoolWithProviderAndAccountFilters_AndPreservesCurrentTarget()
    {
        var context = CreateContext();
        var firstProvider = CreateProvider(context, "Provider One");
        var secondProvider = CreateProvider(context, "Provider Two");
        var firstAccount = CreateAccount(firstProvider.Id, context, "Account One");
        var secondAccount = CreateAccount(secondProvider.Id, context, "Account Two");
        var current = CreateTarget(firstProvider.Id, firstAccount.Id, context, "current", "Current Target");
        var favorite = CreateTarget(secondProvider.Id, secondAccount.Id, context, "favorite", "Favorite Target");

        var definition = new Hive.Agents.AgentDefinition(
            new ResourceEnvelope<Hive.Agents.AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                Hive.Agents.AgentDefinitionId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    context.PrincipalId.Value,
                    Now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(Now)),
            "example-agent",
            "Example Agent",
            Hive.Agents.AgentGeneration.Base,
            current.Id);

        using var editor = new HiveAgentDefinitionEditorForm(
            definition,
            [current, favorite],
            [firstProvider, secondProvider],
            [firstAccount, secondAccount],
            [favorite.Id],
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        Assert.Equal(3, editor.TargetSelector.Items.Count);
        Assert.Contains(
            editor.TargetSelector.Items.Cast<object>(),
            item => item.ToString()!.Contains("Current Target", StringComparison.Ordinal));
        Assert.Contains(
            editor.TargetSelector.Items.Cast<object>(),
            item => item.ToString()!.Contains("Favorite Target", StringComparison.Ordinal));

        editor.ProviderSelector.SelectedIndex = 2;

        Assert.Equal(2, editor.TargetSelector.Items.Count);
        Assert.Contains(
            editor.TargetSelector.Items.Cast<object>(),
            item => item.ToString()!.Contains("Favorite Target", StringComparison.Ordinal));
        Assert.DoesNotContain(
            editor.TargetSelector.Items.Cast<object>(),
            item => item.ToString()!.Contains("Current Target", StringComparison.Ordinal));
    }

    private static ResourceAccessContext CreateContext() =>
        new(DeploymentId.New(), TenantId.New(), PrincipalId.New());

    private static DateTimeOffset Now =>
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static Provider CreateProvider(
        ResourceAccessContext context,
        string displayName)
    {
        var id = ProviderId.New();
        return new Provider(
            new ResourceEnvelope<ProviderId>(
                ResourceKind.Provider,
                id,
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(context.PrincipalId.Value, Now, CorrelationId.New()),
                ResourceLifecycle.Active(Now)),
            $"provider-{id.Value:N}",
            displayName,
            "openai-compatible");
    }

    private static ProviderAccount CreateAccount(
        ProviderId providerId,
        ResourceAccessContext context,
        string displayName)
    {
        var id = ProviderAccountId.New();
        return new ProviderAccount(
            new ResourceEnvelope<ProviderAccountId>(
                ResourceKind.ProviderAccount,
                id,
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(context.PrincipalId.Value, Now, CorrelationId.New()),
                ResourceLifecycle.Active(Now)),
            providerId,
            $"account-{id.Value:N}",
            displayName);
    }

    private static ExecutionTarget CreateTarget(
        ProviderId providerId,
        ProviderAccountId accountId,
        ResourceAccessContext context,
        string key,
        string displayName)
    {
        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                context.PrincipalId!.Value,
                ResourceScope.Tenant(context.TenantId!.Value),
                ResourceVersion.Initial,
                new ResourceProvenance(context.PrincipalId.Value, Now, CorrelationId.New()),
                ResourceLifecycle.Active(Now)),
            providerId,
            accountId,
            $"target-{key}",
            displayName,
            new Uri("https://example.test/v1"),
            $"model-{key}",
            null,
            [
                new CapabilityStateEntry(
                    HiveCapabilityKeys.TextGeneration,
                    CapabilityState.Supported)
            ]);
    }

    private sealed class UiManagementProxy : DispatchProxy
    {
        private IReadOnlyList<Provider> _providers = [];
        private IReadOnlyList<ProviderAccount> _accounts = [];
        private IReadOnlyDictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>> _targets =
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>();
        private IReadOnlyList<ExecutionTargetId> _favorites = [];
        private readonly TaskCompletionSource<ProviderAccountId> _accountLoad =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal static IHiveManagementFacade Create(
            IReadOnlyList<Provider>? providers = null,
            IReadOnlyList<ProviderAccount>? accounts = null,
            IReadOnlyDictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>? targets = null,
            IReadOnlyList<ExecutionTargetId>? favorites = null)
        {
            var management =
                (IHiveManagementFacade)Create<IHiveManagementFacade, UiManagementProxy>();
            var proxy = (UiManagementProxy)(object)management;
            proxy._providers = providers ?? [];
            proxy._accounts = accounts ?? [];
            proxy._targets = targets ?? new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>();
            proxy._favorites = favorites ?? [];
            return management;
        }

        internal async Task WaitForAccountLoadAsync(ProviderAccountId accountId)
        {
            await _accountLoad.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(accountId, _accountLoad.Task.Result);
        }

        protected override object Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IHiveManagementFacade.GetFavoriteExecutionTargetIdsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ExecutionTargetId>>.Success(
                            _favorites)),

                nameof(IHiveManagementFacade.ListProvidersAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(
                            _providers)),

                nameof(IHiveManagementFacade.ListProviderAccountsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ProviderAccount>>.Success(
                            _accounts
                                .Where(account =>
                                    args is not null &&
                                    args.Length > 0 &&
                                    args[0] is ProviderId providerId
                                        ? account.ProviderId == providerId
                                        : true)
                                .ToArray())),

                nameof(IHiveManagementFacade.ListExecutionTargetsAsync) =>
                    HandleTargets(args),

                _ => throw new NotSupportedException(
                    $"The favorite target UI test proxy does not implement '{targetMethod?.Name}'.")
            };
        }

        private object HandleTargets(object?[]? args)
        {
            var accountId = args is not null && args.Length > 0 && args[0] is ProviderAccountId id
                ? id
                : default;

            if (accountId != default)
                _accountLoad.TrySetResult(accountId);

            _targets.TryGetValue(accountId, out var targets);
            return Task.FromResult(
                Result<IReadOnlyList<ExecutionTarget>>.Success(
                    targets ?? []));
        }
    }
}
