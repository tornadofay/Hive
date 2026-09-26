# Phase 0.3 — Identity and Resource Example

This example uses only public Hive.Core contracts.

```csharp
var deploymentId = DeploymentId.New();
var tenantId = TenantId.New();
var principalId = PrincipalId.New();

var createdAt = DateTimeOffset.UtcNow;

var workItem = WorkItem.Create(
    WorkItemId.New(),
    principalId,
    ResourceScope.Tenant(tenantId),
    new ResourceProvenance(
        principalId,
        createdAt,
        CorrelationId.New()),
    createdAt);

var accessContext = new ResourceAccessContext(
    deploymentId,
    tenantId,
    principalId);

bool inScope = workItem.Resource.Scope.Matches(accessContext);

var queued = workItem.TransitionTo(
    WorkItemStatus.Queued,
    createdAt.AddSeconds(1));

Console.WriteLine($"WorkItem: {queued.Id}");
Console.WriteLine($"In scope: {inScope}");
Console.WriteLine($"Version: {queued.Resource.Version}");
```

Expected result:

- `inScope` is `true`;
- the WorkItem identity is unchanged by the status transition;
- the WorkItem resource version changes from `1` to `2`;
- the original WorkItem remains in `Created` status;
- the returned WorkItem is in `Queued` status;
- owner, scope, and provenance remain unchanged.

A missing required identity in `ResourceAccessContext` causes scope matching to return `false`; it does not grant access by default.
