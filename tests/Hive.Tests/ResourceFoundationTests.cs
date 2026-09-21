using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class ResourceFoundationTests
{
    [Fact]
    public void ScopeMatrix_MatchesTheRequiredIdentityBoundary()
    {
        var deploymentId = DeploymentId.New();
        var tenantId = TenantId.New();
        var principalId = PrincipalId.New();
        var userId = UserId.New();
        var workspaceId = WorkspaceId.New();
        var agentId = AgentId.New();
        var runtimeId = RuntimeId.New();
        var executionId = ExecutionId.New();

        var context = new ResourceAccessContext(
            deploymentId,
            tenantId,
            principalId,
            userId,
            SessionId.New(),
            workspaceId,
            agentId,
            HiveId.New(),
            runtimeId,
            executionId);

        Assert.True(ResourceScope.Global().Matches(context));
        Assert.True(ResourceScope.Tenant(tenantId).Matches(context));
        Assert.True(ResourceScope.User(userId).Matches(context));
        Assert.True(ResourceScope.Workspace(workspaceId).Matches(context));
        Assert.True(ResourceScope.Agent(agentId).Matches(context));
        Assert.True(ResourceScope.Runtime(runtimeId).Matches(context));
        Assert.True(ResourceScope.Execution(executionId).Matches(context));
    }

    [Fact]
    public void ScopeMatrix_RejectsWrongIdentityAtEachBound()
    {
        var context = new ResourceAccessContext(
            DeploymentId.New(),
            TenantId.New(),
            PrincipalId.New(),
            UserId.New(),
            null,
            WorkspaceId.New(),
            AgentId.New(),
            null,
            RuntimeId.New(),
            ExecutionId.New());

        Assert.False(ResourceScope.Tenant(TenantId.New()).Matches(context));
        Assert.False(ResourceScope.User(UserId.New()).Matches(context));
        Assert.False(ResourceScope.Workspace(WorkspaceId.New()).Matches(context));
        Assert.False(ResourceScope.Agent(AgentId.New()).Matches(context));
        Assert.False(ResourceScope.Runtime(RuntimeId.New()).Matches(context));
        Assert.False(ResourceScope.Execution(ExecutionId.New()).Matches(context));
    }

    [Fact]
    public void ScopeMatching_FailsClosedWhenRequiredIdentityIsMissing()
    {
        var deploymentId = DeploymentId.New();
        var tenantId = TenantId.New();
        var principalId = PrincipalId.New();
        var userId = UserId.New();
        var workspaceId = WorkspaceId.New();
        var agentId = AgentId.New();
        var runtimeId = RuntimeId.New();
        var executionId = ExecutionId.New();

        Assert.False(ResourceScope.Global().Matches(new ResourceAccessContext(
            deploymentId, null, principalId)));

        Assert.False(ResourceScope.Tenant(tenantId).Matches(new ResourceAccessContext(
            deploymentId, null, principalId)));

        Assert.False(ResourceScope.Tenant(tenantId).Matches(new ResourceAccessContext(
            deploymentId, tenantId, null)));

        Assert.False(ResourceScope.User(userId).Matches(new ResourceAccessContext(
            deploymentId, null, principalId, userId)));

        Assert.False(ResourceScope.Workspace(workspaceId).Matches(new ResourceAccessContext(
            deploymentId, null, principalId, null, null, workspaceId)));

        Assert.False(ResourceScope.Agent(agentId).Matches(new ResourceAccessContext(
            deploymentId, null, principalId, null, null, null, agentId)));

        Assert.False(ResourceScope.Runtime(runtimeId).Matches(new ResourceAccessContext(
            deploymentId, tenantId, principalId, null, null, null, null, null, runtimeId)));

        Assert.False(ResourceScope.Execution(executionId).Matches(new ResourceAccessContext(
            deploymentId, tenantId, principalId, null, null, null, agentId, null, null, executionId)));
    }

    [Fact]
    public void ResourceEnvelope_CopiesMetadataAndExposesImmutableSnapshot()
    {
        var metadata = new Dictionary<string, string>
        {
            ["key"] = "before"
        };

        var resource = new ResourceEnvelope<AgentId>(
            ResourceKind.Agent,
            AgentId.New(),
            PrincipalId.New(),
            ResourceScope.Tenant(TenantId.New()),
            ResourceVersion.Initial,
            new ResourceProvenance(
                PrincipalId.New(),
                DateTimeOffset.UtcNow,
                CorrelationId.New()),
            ResourceLifecycle.Active(DateTimeOffset.UtcNow),
            metadata);

        metadata["key"] = "after";

        var snapshot = resource.Snapshot();
        var updated = resource.WithMetadata("other", "value");

        Assert.Equal("before", resource.Metadata["key"]);
        Assert.Equal(resource.Kind, snapshot.Kind);
        Assert.Equal(resource.Identity, snapshot.Identity);
        Assert.Equal(ResourceVersion.Initial, snapshot.Version);
        Assert.Equal(new ResourceVersion(2), updated.Version);
        Assert.DoesNotContain("other", resource.Metadata.Keys);
    }

    [Fact]
    public void ResourceEnvelope_RejectsMissingOwnerAndCreatingPrincipal()
    {
        var now = DateTimeOffset.UtcNow;
        var scope = ResourceScope.Global();

        Assert.Throws<ArgumentException>(() =>
            new ResourceEnvelope<AgentId>(
                ResourceKind.Agent,
                AgentId.New(),
                default,
                scope,
                ResourceVersion.Initial,
                new ResourceProvenance(
                    PrincipalId.New(),
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)));

        Assert.Throws<ArgumentException>(() =>
            new ResourceProvenance(
                default,
                now,
                CorrelationId.New()));

        Assert.Throws<ArgumentException>(() =>
            new ResourceProvenance(
                PrincipalId.New(),
                now,
                default));
    }

    [Fact]
    public void ResourceLifecycle_StopsReactivationAfterRetirement()
    {
        var now = DateTimeOffset.UtcNow;
        var lifecycle = ResourceLifecycle.Active(now)
            .TransitionTo(ResourceLifecycleStatus.Suspended, now.AddMinutes(1))
            .TransitionTo(ResourceLifecycleStatus.Retired, now.AddMinutes(2));

        Assert.Equal(ResourceLifecycleStatus.Retired, lifecycle.Status);

        Assert.Throws<InvalidOperationException>(() =>
            lifecycle.TransitionTo(ResourceLifecycleStatus.Active, now.AddMinutes(3)));
    }

    [Fact]
    public void ResourceVersion_IsPositiveAndMonotonic()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceVersion(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ResourceVersion(-1));
        Assert.Equal(2, ResourceVersion.Initial.Next().Value);
        Assert.Throws<InvalidOperationException>(() => new ResourceVersion(long.MaxValue).Next());
    }
}
