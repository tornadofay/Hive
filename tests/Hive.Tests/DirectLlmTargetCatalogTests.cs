using System.Reflection;
using Hive.Core;
using Hive.Host.WinForms;
using Hive.Management;
using Xunit;

namespace Hive.Tests;

public sealed class DirectLlmTargetCatalogTests
{
    [Fact]
    public async Task NoFavorites_ListsEveryEligibleTarget()
    {
        var fixture = CreateFixture();
        var management = CatalogProxy.Create(
            fixture.Providers,
            fixture.Accounts,
            fixture.Targets,
            Result<IReadOnlyList<ExecutionTargetId>>.Success(Array.Empty<ExecutionTargetId>()));

        var result = await DirectLlmTargetCatalog.LoadAsync(
            management,
            fixture.Context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectLlmTargetSource.AllEligible, result.Value!.Source);
        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            result.Value.Targets.Select(option => option.Target.DisplayName));
    }

    [Fact]
    public async Task Favorites_RestrictTheListToFavoritesOnly()
    {
        var fixture = CreateFixture();
        var management = CatalogProxy.Create(
            fixture.Providers,
            fixture.Accounts,
            fixture.Targets,
            Result<IReadOnlyList<ExecutionTargetId>>.Success([fixture.Beta.Id]));

        var result = await DirectLlmTargetCatalog.LoadAsync(
            management,
            fixture.Context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectLlmTargetSource.Favorites, result.Value!.Source);
        Assert.Equal(1, result.Value.FavoriteCount);
        Assert.Equal(
            ["Beta"],
            result.Value.Targets.Select(option => option.Target.DisplayName));
    }

    [Fact]
    public async Task FavoritesSetButNoneEligible_ReturnsAnEmptyListWithoutFallingBack()
    {
        var fixture = CreateFixture();
        var management = CatalogProxy.Create(
            fixture.Providers,
            fixture.Accounts,
            fixture.Targets,
            Result<IReadOnlyList<ExecutionTargetId>>.Success([ExecutionTargetId.New()]));

        var result = await DirectLlmTargetCatalog.LoadAsync(
            management,
            fixture.Context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectLlmTargetSource.Favorites, result.Value!.Source);
        Assert.Empty(result.Value.Targets);
        Assert.Contains(
            "favorite",
            result.Value.DescribeStatus(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task FavoritesLoadFailure_FailsClosedInsteadOfListingEverything()
    {
        var fixture = CreateFixture();
        var management = CatalogProxy.Create(
            fixture.Providers,
            fixture.Accounts,
            fixture.Targets,
            Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                Error.Validation(
                    "hive.tests.favorites-failed",
                    "The favorites store failed.")));

        var result = await DirectLlmTargetCatalog.LoadAsync(
            management,
            fixture.Context,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("hive.tests.favorites-failed", result.Error!.Code);
    }

    [Fact]
    public async Task FavoritesStoreNotConfigured_IsTreatedAsNoFavorites()
    {
        var fixture = CreateFixture();
        var management = CatalogProxy.Create(
            fixture.Providers,
            fixture.Accounts,
            fixture.Targets,
            Result<IReadOnlyList<ExecutionTargetId>>.Failure(
                Error.Unsupported(
                    DirectLlmTargetCatalog.FavoritesUnavailableErrorCode,
                    "Execution target favorites persistence is not configured.")));

        var result = await DirectLlmTargetCatalog.LoadAsync(
            management,
            fixture.Context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(DirectLlmTargetSource.AllEligible, result.Value!.Source);
        Assert.Equal(3, result.Value.Targets.Count);
    }

    [Fact]
    public async Task IneligibleTargetsAndProvidersAreNeverListed()
    {
        var fixture = CreateFixture();
        var otherProvider = CreateProvider(fixture.Context, "Other Provider", "other-transport");
        var otherAccount = CreateAccount(otherProvider.Id, fixture.Context, "Other Account");
        var otherTarget = CreateTarget(
            otherProvider.Id,
            otherAccount.Id,
            fixture.Context,
            "other",
            "Other Transport Target",
            CapabilityState.Supported);
        var noText = CreateTarget(
            fixture.Provider.Id,
            fixture.Account.Id,
            fixture.Context,
            "no-text",
            "No Text Generation",
            CapabilityState.Unsupported);

        var targets = new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
        {
            [fixture.Account.Id] =
            [
                fixture.Alpha,
                fixture.Beta,
                fixture.Gamma,
                noText
            ],
            [otherAccount.Id] = [otherTarget]
        };

        var management = CatalogProxy.Create(
            [fixture.Provider, otherProvider],
            [fixture.Account, otherAccount],
            targets,
            Result<IReadOnlyList<ExecutionTargetId>>.Success(Array.Empty<ExecutionTargetId>()));

        var result = await DirectLlmTargetCatalog.LoadAsync(
            management,
            fixture.Context,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            result.Value!.Targets.Select(option => option.Target.DisplayName));
    }

    [Fact]
    public void StatusMessages_DescribeTheActiveSource()
    {
        var none = new DirectLlmTargetCatalogResult(
            Array.Empty<DirectLlmTargetOption>(),
            DirectLlmTargetSource.AllEligible,
            0);
        var unusable = new DirectLlmTargetCatalogResult(
            Array.Empty<DirectLlmTargetOption>(),
            DirectLlmTargetSource.Favorites,
            2);

        Assert.Contains("Configure one in Settings", none.DescribeStatus(), StringComparison.Ordinal);
        Assert.Contains("2 favorite", unusable.DescribeStatus(), StringComparison.Ordinal);
    }

    private static Fixture CreateFixture()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New());
        var provider = CreateProvider(context, "Provider One", "openai-compatible");
        var account = CreateAccount(provider.Id, context, "Account One");
        var alpha = CreateTarget(provider.Id, account.Id, context, "alpha", "Alpha", CapabilityState.Supported);
        var beta = CreateTarget(provider.Id, account.Id, context, "beta", "Beta", CapabilityState.Supported);
        var gamma = CreateTarget(provider.Id, account.Id, context, "gamma", "Gamma", CapabilityState.Supported);

        return new Fixture(
            context,
            provider,
            account,
            alpha,
            beta,
            gamma);
    }

    private static DateTimeOffset Now =>
        new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    private static Provider CreateProvider(
        ResourceAccessContext context,
        string displayName,
        string transportKind)
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
            transportKind);
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
        string displayName,
        CapabilityState textGeneration) =>
        new(
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
                    textGeneration)
            ]);

    private sealed class Fixture
    {
        public Fixture(
            ResourceAccessContext context,
            Provider provider,
            ProviderAccount account,
            ExecutionTarget alpha,
            ExecutionTarget beta,
            ExecutionTarget gamma)
        {
            Context = context;
            Provider = provider;
            Account = account;
            Alpha = alpha;
            Beta = beta;
            Gamma = gamma;
        }

        public ResourceAccessContext Context { get; }

        public Provider Provider { get; }

        public ProviderAccount Account { get; }

        public ExecutionTarget Alpha { get; }

        public ExecutionTarget Beta { get; }

        public ExecutionTarget Gamma { get; }

        public IReadOnlyList<Provider> Providers => [Provider];

        public IReadOnlyList<ProviderAccount> Accounts => [Account];

        public IReadOnlyDictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>> Targets =>
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>
            {
                [Account.Id] = [Alpha, Beta, Gamma]
            };
    }

    private class CatalogProxy : DispatchProxy
    {
        private IReadOnlyList<Provider> _providers = [];
        private IReadOnlyList<ProviderAccount> _accounts = [];
        private IReadOnlyDictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>> _targets =
            new Dictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>>();
        private Result<IReadOnlyList<ExecutionTargetId>> _favorites =
            Result<IReadOnlyList<ExecutionTargetId>>.Success(Array.Empty<ExecutionTargetId>());

        internal static IHiveManagementFacade Create(
            IReadOnlyList<Provider> providers,
            IReadOnlyList<ProviderAccount> accounts,
            IReadOnlyDictionary<ProviderAccountId, IReadOnlyList<ExecutionTarget>> targets,
            Result<IReadOnlyList<ExecutionTargetId>> favorites)
        {
            var management =
                (IHiveManagementFacade)Create<IHiveManagementFacade, CatalogProxy>();
            var proxy = (CatalogProxy)(object)management;
            proxy._providers = providers;
            proxy._accounts = accounts;
            proxy._targets = targets;
            proxy._favorites = favorites;
            return management;
        }

        protected override object Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            return targetMethod?.Name switch
            {
                nameof(IHiveManagementFacade.GetFavoriteExecutionTargetIdsAsync) =>
                    Task.FromResult(_favorites),

                nameof(IHiveManagementFacade.ListProvidersAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<Provider>>.Success(_providers)),

                nameof(IHiveManagementFacade.ListProviderAccountsAsync) =>
                    Task.FromResult(
                        Result<IReadOnlyList<ProviderAccount>>.Success(
                            _accounts
                                .Where(account =>
                                    args is null ||
                                    args.Length == 0 ||
                                    args[0] is not ProviderId providerId ||
                                    account.ProviderId == providerId)
                                .ToArray())),

                nameof(IHiveManagementFacade.ListExecutionTargetsAsync) =>
                    HandleTargets(args),

                _ => throw new NotSupportedException(
                    $"The catalog test proxy does not implement '{targetMethod?.Name}'.")
            };
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
