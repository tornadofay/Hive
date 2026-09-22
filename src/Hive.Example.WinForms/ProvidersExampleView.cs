using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Host.WinForms.UI.Theme;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class ProvidersExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly HiveDatabaseOptions _databaseOptions;
    private readonly IHiveExampleOutput _output;

    public ProvidersExampleView(
        HiveDatabaseOptions databaseOptions,
        IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);
        ArgumentNullException.ThrowIfNull(output);

        _databaseOptions = databaseOptions;
        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run provider CRUD"
        };

        _surface.SetInformation(
            "Creates a Provider, ProviderAccount, and ExecutionTarget through the public Hive.Persistence API, then reads, lists, updates, checks ownership/scope, and retires the target. No provider credentials are required for this example.",
            "The run completes without a provider network call. The output shows the persisted resource identities, version changes, capability states, ownership/scope checks, and the retired target.",
            "Database",
            $"SQL Server database: {_databaseOptions.DatabaseName}");

        _surface.CodeSnippet = """
            var options = HiveDatabaseOptions.LocalDevelopment();
            var store = new SqlProviderResourceStore(options);

            // Create Provider → ProviderAccount → ExecutionTarget,
            // then use the store's public CRUD methods.
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        var migration = await new HiveDatabaseMigrator(
                _databaseOptions)
            .MigrateAsync(cancellationToken);

        if (migration.IsFailure)
            throw new InvalidOperationException(
                $"Hive database migration failed: {migration.Error?.Message}");

        cancellationToken.ThrowIfCancellationRequested();

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var store = new SqlProviderResourceStore(_databaseOptions);

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
            $"example-{Guid.NewGuid():N}",
            "Example Provider",
            "openai-compatible");

        var providerResult = await store.CreateProviderAsync(
            provider,
            context,
            cancellationToken);

        EnsureSuccess(providerResult, "Provider creation");

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
            $"example-account-{Guid.NewGuid():N}",
            "Example Account",
            "example-account");

        var accountResult = await store.CreateProviderAccountAsync(
            account,
            context,
            cancellationToken);

        EnsureSuccess(accountResult, "ProviderAccount creation");

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
            $"example-target-{Guid.NewGuid():N}",
            "Example Target",
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported),
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unknown)
            ]);

        var targetResult = await store.CreateExecutionTargetAsync(
            target,
            context,
            cancellationToken);

        EnsureSuccess(targetResult, "ExecutionTarget creation");

        var loadedTarget = await store.GetExecutionTargetAsync(
            target.Id,
            context,
            cancellationToken);

        EnsureSuccess(loadedTarget, "ExecutionTarget read");

        var providerUpdate = await store.UpdateProviderAsync(
            provider.WithDisplayName("Example Provider Updated"),
            context,
            cancellationToken);

        EnsureSuccess(providerUpdate, "Provider update");

        var accountUpdate = await store.UpdateProviderAccountAsync(
            account.WithExternalAccountId("example-account-updated"),
            context,
            cancellationToken);

        EnsureSuccess(accountUpdate, "ProviderAccount update");

        var targetUpdate = await store.UpdateExecutionTargetAsync(
            target.WithDisplayName("Example Target Updated"),
            context,
            cancellationToken);

        EnsureSuccess(targetUpdate, "ExecutionTarget update");

        var ownerCheck = await store.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                tenant,
                PrincipalId.New()),
            cancellationToken);

        if (ownerCheck.IsSuccess ||
            ownerCheck.Error?.Category != ErrorCategory.Forbidden)
        {
            throw new InvalidOperationException(
                "Owner isolation check did not return Forbidden.");
        }

        var scopeCheck = await store.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                principal),
            cancellationToken);

        if (scopeCheck.IsSuccess ||
            scopeCheck.Error?.Category != ErrorCategory.Forbidden)
        {
            throw new InvalidOperationException(
                "Scope isolation check did not return Forbidden.");
        }

        var retiredTarget = await store.DeleteExecutionTargetAsync(
            target.Id,
            context,
            cancellationToken);

        EnsureSuccess(retiredTarget, "ExecutionTarget retirement");

        _output.Write(
            "Provider Platform Example",
            $"""
            Migration: {migration.Value!.Status}; schema {migration.Value.CurrentSchemaVersion}
            Provider: {provider.Id}; version {providerUpdate.Value!.Resource.Version}
            ProviderAccount: {account.Id}; version {accountUpdate.Value!.Resource.Version}
            ExecutionTarget: {target.Id}; loaded model={loadedTarget.Value!.Model}
            Capabilities: {string.Join(", ", loadedTarget.Value.Capabilities.Select(
                capability => $"{capability.Capability}={capability.State}"))}
            Ownership check: Forbidden
            Scope check: Forbidden
            Retired target: {retiredTarget.Value!.Resource.Lifecycle.Status}
            """);
    }

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
}
