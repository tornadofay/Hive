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
    public async Task FavoriteSettings_ShowsOnlyStoredFavorites()
    {
        var context = CreateContext();
        var provider = CreateProvider(context, "Provider One");
        var account = CreateAccount(provider.Id, context, "Account One");
        var first = CreateTarget(provider.Id, account.Id, context, "first", "First Target");
        var second = CreateTarget(provider.Id, account.Id, context, "second", "Second Target");
        var third = CreateTarget(provider.Id, account.Id, context, "third", "Third Target");

        var management = UiManagementProxy.Create(
            [provider],
            [account],
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [account.Id] = [first, second, third]
            },
            [second.Id]);

        using var view = new HiveFavoriteExecutionTargetsSettingsView(
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        await view.InitializeAsync();

        Assert.Single(view.FavoriteList.Items);
        var item = (ListViewItem)view.FavoriteList.Items[0];
        Assert.Equal(second.Id, item.Tag);
        Assert.Equal("Second Target", item.Text);
        Assert.DoesNotContain(
            view.FavoriteList.Items.Cast<ListViewItem>(),
            candidate => candidate.Tag is ExecutionTargetId id && id == first.Id);
        Assert.DoesNotContain(
            view.FavoriteList.Items.Cast<ListViewItem>(),
            candidate => candidate.Tag is ExecutionTargetId id && id == third.Id);
        Assert.False(view.FavoriteList.CheckBoxes);
    }

    [WinFormsFact]
    public async Task FavoriteSettings_EmptyStateDoesNotLoadTargetCatalog()
    {
        var context = CreateContext();
        var provider = CreateProvider(context, "Provider One");
        var account = CreateAccount(provider.Id, context, "Account One");

        var management = UiManagementProxy.Create(
            [provider],
            [account],
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [account.Id] = []
            });

        using var view = new HiveFavoriteExecutionTargetsSettingsView(
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        await view.InitializeAsync();

        Assert.Empty(view.FavoriteList.Items);
        Assert.Contains(
            "No favorite execution targets configured.",
            view.StatusLabel.Text,
            StringComparison.Ordinal);
    }

    [WinFormsFact]
    public async Task FavoriteTargetPicker_UsesProviderAndAccountFiltersBeforeShowingTargets()
    {
        var context = CreateContext();
        var firstProvider = CreateProvider(context, "Provider One");
        var secondProvider = CreateProvider(context, "Provider Two");
        var firstAccount = CreateAccount(firstProvider.Id, context, "Account One");
        var secondAccount = CreateAccount(secondProvider.Id, context, "Account Two");
        var firstTarget = CreateTarget(
            firstProvider.Id,
            firstAccount.Id,
            context,
            "first",
            "First Target");
        var secondTarget = CreateTarget(
            secondProvider.Id,
            secondAccount.Id,
            context,
            "second",
            "Second Target");

        var management = UiManagementProxy.Create(
            [firstProvider, secondProvider],
            [firstAccount, secondAccount],
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [firstAccount.Id] = [firstTarget],
                [secondAccount.Id] = [secondTarget]
            },
            [firstTarget.Id]);

        using var picker = new HiveFavoriteExecutionTargetPickerForm(
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light),
            new HashSet<ExecutionTargetId> { firstTarget.Id });

        await picker.InitializeAsync();

        Assert.Equal(2, picker.ProviderSelector.Items.Count);
        Assert.Equal("Provider One", picker.ProviderSelector.SelectedItem!.ToString());
        Assert.Single(picker.AccountSelector.Items);
        Assert.Equal("Account One", picker.AccountSelector.SelectedItem!.ToString());
        Assert.Empty(picker.TargetSelector.Items.Cast<object>());
        Assert.False(picker.AddButton.Enabled);
    }

    [WinFormsFact]
    public async Task FavoriteSettings_RepeatedRefreshDoesNotThrow()
    {
        var context = CreateContext();
        var provider = CreateProvider(context, "Provider One");
        var account = CreateAccount(provider.Id, context, "Account One");
        var target = CreateTarget(provider.Id, account.Id, context, "target", "Target");

        var management = UiManagementProxy.Create(
            [provider],
            [account],
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [account.Id] = [target]
            },
            [target.Id]);

        using var view = new HiveFavoriteExecutionTargetsSettingsView(
            management,
            context,
            new HiveThemeManager(HiveThemeMode.Light));

        await view.InitializeAsync();
        await view.InitializeAsync();
        await view.InitializeAsync();

        Assert.Single(view.FavoriteList.Items);
        Assert.Equal(target.Id, ((ListViewItem)view.FavoriteList.Items[0]).Tag);
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

                nameof(IHiveManagementFacade.ReplaceFavoriteExecutionTargetIdsAsync) =>
                    ReplaceFavorites(args),

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

        private object ReplaceFavorites(object?[]? args)
        {
            var ids = args is not null &&
                      args.Length > 0 &&
                      args[0] is IReadOnlyList<ExecutionTargetId> list
                ? list
                : Array.Empty<ExecutionTargetId>();

            _favorites = ids.ToArray();

            return Task.FromResult(
                Result<IReadOnlyList<ExecutionTargetId>>.Success(_favorites));
        }

        private object HandleTargets(object?[]? args)
        {
            var accountId = args is not null && args.Length > 0 && args[0] is ProviderAccountId id
                ? id
                : default;

            _targets.TryGetValue(accountId, out var targets);
            return Task.FromResult(
                Result<IReadOnlyList<ExecutionTarget>>.Success(
                    targets ?? []));
        }
    }
}
