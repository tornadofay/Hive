using Hive.Agents;
using Hive.Core;
using Hive.Host.WinForms.UI.Controls;
using Hive.Management;
using Hive.Persistence;

namespace Hive.Example.WinForms;

internal sealed class HiveManagementFacadeExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IServiceProvider _services;

    public HiveManagementFacadeExampleView(IServiceProvider services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run Management CRUD"
        };

        _surface.SetInformation(
            "Exercises the public Hive.Management facade for Provider, ProviderAccount, ExecutionTarget, and AgentDefinition CRUD.",
            "The scenario uses the Management facade for domain operations, verifies ownership/scope isolation and optimistic concurrency, and retires the AgentDefinition.",
            "Boundary",
            "Hive.Example.WinForms → Hive.Management → Hive.Persistence");

        _surface.CodeSnippet = """
            var management = services.GetManagementFacade();

            await management.CreateProviderAsync(...);
            await management.CreateAgentDefinitionAsync(...);
            await management.UpdateProviderAsync(...);
            await management.DeleteAgentDefinitionAsync(...);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            services.GetExampleOutput(),
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private async Task RunExampleAsync(CancellationToken cancellationToken)
    {
        var options = HiveDatabaseOptions.LocalDevelopment();
        var migration = await new HiveDatabaseMigrator(options)
            .MigrateAsync(cancellationToken);

        if (migration.IsFailure)
        {
            throw new InvalidOperationException(
                $"Hive database migration failed: {migration.Error?.Message}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            tenant,
            principal);

        var management = _services.GetManagementFacade();

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
            $"management-example-provider-{Guid.NewGuid():N}",
            "Management Example Provider",
            "openai-compatible");

        var createdProvider = await management.CreateProviderAsync(
            provider,
            context,
            cancellationToken);
        EnsureSuccess(createdProvider, "Provider creation");

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
            $"management-example-account-{Guid.NewGuid():N}",
            "Management Example Account");

        var createdAccount = await management.CreateProviderAccountAsync(
            account,
            context,
            cancellationToken);
        EnsureSuccess(createdAccount, "ProviderAccount creation");

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
            $"management-example-target-{Guid.NewGuid():N}",
            "Management Example Target",
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported)
            ]);

        var createdTarget = await management.CreateExecutionTargetAsync(
            target,
            context,
            cancellationToken);
        EnsureSuccess(createdTarget, "ExecutionTarget creation");

        var definition = new AgentDefinition(
            new ResourceEnvelope<AgentDefinitionId>(
                ResourceKind.AgentDefinition,
                AgentDefinitionId.New(),
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            $"management-example-agent-{Guid.NewGuid():N}",
            "Management Example Agent",
            AgentGeneration.Base);

        var createdDefinition = await management.CreateAgentDefinitionAsync(
            definition,
            context,
            cancellationToken);
        EnsureSuccess(createdDefinition, "AgentDefinition creation");

        var updatedProvider = await management.UpdateProviderAsync(
            provider.WithDisplayName("Management Example Provider Updated"),
            context,
            cancellationToken);
        EnsureSuccess(updatedProvider, "Provider update");

        var definitions = await management.ListAgentDefinitionsAsync(
            context,
            cancellationToken: cancellationToken);
        EnsureSuccess(definitions, "AgentDefinition list");

        var ownerIsolation = await management.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                tenant,
                PrincipalId.New()),
            cancellationToken);

        EnsureForbidden(
            ownerIsolation,
            "Management ownership check");

        var scopeIsolation = await management.GetProviderAsync(
            provider.Id,
            new ResourceAccessContext(
                context.DeploymentId,
                TenantId.New(),
                principal),
            cancellationToken);

        EnsureForbidden(
            scopeIsolation,
            "Management scope check");

        var stale = await management.UpdateProviderAsync(
            provider.WithTransportKind("stale-transport"),
            context,
            cancellationToken);

        if (stale.IsSuccess ||
            stale.Error?.Category != ErrorCategory.Concurrency)
        {
            throw new InvalidOperationException(
                "Management concurrency check did not return Concurrency.");
        }

        var retired = await management.DeleteAgentDefinitionAsync(
            definition.Id,
            context,
            cancellationToken);
        EnsureSuccess(retired, "AgentDefinition retirement");

        _services.GetExampleOutput().Write(
            "Hive.Management CRUD Facade",
            $"""
            Migration: {migration.Value!.Status}; schema {migration.Value.CurrentSchemaVersion}
            Provider: {provider.Id}; version {updatedProvider.Value!.Resource.Version}
            ProviderAccount: {account.Id}
            ExecutionTarget: {target.Id}
            AgentDefinition: {definition.Id}; created version {createdDefinition.Value!.Resource!.Version}
            AgentDefinitions listed before retirement: {definitions.Value!.Count}
            Ownership check: Forbidden
            Scope check: Forbidden
            Concurrency check: Concurrency
            AgentDefinition lifecycle: {retired.Value!.Resource!.Lifecycle.Status}
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

    private static void EnsureForbidden<T>(
        Result<T> result,
        string operation)
    {
        if (result.IsSuccess ||
            result.Error?.Category != ErrorCategory.Forbidden)
        {
            throw new InvalidOperationException(
                $"{operation} did not return Forbidden.");
        }
    }
}
