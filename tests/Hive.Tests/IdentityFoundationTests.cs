using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class IdentityFoundationTests
{
    [Fact]
    public void AllPhase03IdentityTypes_CreateNonEmptyValues()
    {
        Assert.NotEqual(Guid.Empty, DeploymentId.New().Value);
        Assert.NotEqual(Guid.Empty, TenantId.New().Value);
        Assert.NotEqual(Guid.Empty, PrincipalId.New().Value);
        Assert.NotEqual(Guid.Empty, UserId.New().Value);
        Assert.NotEqual(Guid.Empty, SessionId.New().Value);
        Assert.NotEqual(Guid.Empty, WorkspaceId.New().Value);
        Assert.NotEqual(Guid.Empty, AgentId.New().Value);
        Assert.NotEqual(Guid.Empty, HiveId.New().Value);
        Assert.NotEqual(Guid.Empty, RuntimeId.New().Value);
        Assert.NotEqual(Guid.Empty, ExecutionId.New().Value);
        Assert.NotEqual(Guid.Empty, WorkItemId.New().Value);
    }

    [Fact]
    public void RepresentativeTypedIds_RoundTripThroughString()
    {
        var value = Guid.NewGuid();

        var deployment = DeploymentId.Parse(value.ToString("D"));
        var tenant = TenantId.Parse(value.ToString("D"));
        var principal = PrincipalId.Parse(value.ToString("D"));
        var agent = AgentId.Parse(value.ToString("D"));
        var workItem = WorkItemId.Parse(value.ToString("D"));

        Assert.Equal(value, deployment.Value);
        Assert.Equal(value, tenant.Value);
        Assert.Equal(value, principal.Value);
        Assert.Equal(value, agent.Value);
        Assert.Equal(value, workItem.Value);

        Assert.True(DeploymentId.TryParse(deployment.ToString(), out var parsedDeployment));
        Assert.True(TenantId.TryParse(tenant.ToString(), out var parsedTenant));
        Assert.True(PrincipalId.TryParse(principal.ToString(), out var parsedPrincipal));
        Assert.True(AgentId.TryParse(agent.ToString(), out var parsedAgent));
        Assert.True(WorkItemId.TryParse(workItem.ToString(), out var parsedWorkItem));

        Assert.Equal(deployment, parsedDeployment);
        Assert.Equal(tenant, parsedTenant);
        Assert.Equal(principal, parsedPrincipal);
        Assert.Equal(agent, parsedAgent);
        Assert.Equal(workItem, parsedWorkItem);
    }

    [Fact]
    public void InvalidIdentityValues_AreRejected()
    {
        Assert.Throws<ArgumentException>(() => new DeploymentId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new PrincipalId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new UserId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new SessionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new WorkspaceId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new AgentId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new HiveId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new RuntimeId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new ExecutionId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new WorkItemId(Guid.Empty));

        Assert.False(DeploymentId.TryParse(null, out _));
        Assert.False(TenantId.TryParse("not-a-guid", out _));
        Assert.False(WorkItemId.TryParse(Guid.Empty.ToString("D"), out _));
    }
}
