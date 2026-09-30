# Phase 1.1 — Provider / ProviderAccount / ExecutionTarget example

This example exercises the public Hive.Core resource contracts and Hive.Persistence.SqlProviderResourceStore without calling a real provider.

```csharp
using Hive.Core;
using Hive.Persistence;

var options = HiveDatabaseOptions.LocalDevelopment();
var migrator = new HiveDatabaseMigrator(options);
var migration = await migrator.MigrateAsync();

var principal = PrincipalId.New();
var tenant = TenantId.New();
var context = new ResourceAccessContext(DeploymentId.New(), tenant, principal);
var now = DateTimeOffset.UtcNow;

var provider = new Provider(
    new ResourceEnvelope<ProviderId>(
        ResourceKind.Provider, ProviderId.New(), principal,
        ResourceScope.Tenant(tenant), ResourceVersion.Initial,
        new ResourceProvenance(principal, now, CorrelationId.New()),
        ResourceLifecycle.Active(now)),
    "example-provider",
    "Example Provider",
    "openai-compatible");

var store = new SqlProviderResourceStore(options);
var createdProvider = await store.CreateProviderAsync(provider, context);

var account = new ProviderAccount(
    new ResourceEnvelope<ProviderAccountId>(
        ResourceKind.ProviderAccount, ProviderAccountId.New(), principal,
        ResourceScope.Tenant(tenant), ResourceVersion.Initial,
        new ResourceProvenance(principal, now, CorrelationId.New()),
        ResourceLifecycle.Active(now)),
    provider.Id,
    "example-account",
    "Example Account");

var createdAccount = await store.CreateProviderAccountAsync(account, context);

var target = new ExecutionTarget(
    new ResourceEnvelope<ExecutionTargetId>(
        ResourceKind.ExecutionTarget, ExecutionTargetId.New(), principal,
        ResourceScope.Tenant(tenant), ResourceVersion.Initial,
        new ResourceProvenance(principal, now, CorrelationId.New()),
        ResourceLifecycle.Active(now)),
    provider.Id, account.Id,
    "example-target",
    "Example Target",
    new Uri("https://example.test/v1"),
    "example-model",
    null,
    [
        new CapabilityStateEntry(new CapabilityKey("text.generate"), CapabilityState.Supported),
        new CapabilityStateEntry(new CapabilityKey("vision"), CapabilityState.Unknown)
    ]);

var createdTarget = await store.CreateExecutionTargetAsync(target, context);

Console.WriteLine(createdProvider.IsSuccess);
Console.WriteLine(createdAccount.IsSuccess);
Console.WriteLine(createdTarget.IsSuccess);
```

Expected result:

- the migration reports schema version `2`;
- Provider, ProviderAccount, and ExecutionTarget create successfully;
- the target preserves explicit `Supported` and `Unknown` capability states;
- reads/lists honor owner and scope;
- updates increment the resource version;
- a stale update returns a `Concurrency` error;
- delete retires the resource instead of erasing its durable identity;