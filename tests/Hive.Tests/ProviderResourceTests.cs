using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class ProviderResourceTests
{
    [Fact]
    public void Provider_AcceptsBoundaryLengthsAndPreservesIdentity()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var providerId = ProviderId.New();
        var provider = CreateProvider(
            providerId,
            principal,
            tenant,
            new string('k', 100),
            new string('d', 200));

        Assert.Equal(providerId, provider.Id);
        Assert.Equal(100, provider.Key.Length);
        Assert.Equal(200, provider.DisplayName.Length);
    }

    [Fact]
    public void Provider_RejectsWrongResourceKind()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var resource = CreateEnvelope(
            ResourceKind.Agent,
            ProviderId.New(),
            principal,
            ResourceScope.Tenant(tenant));

        Assert.Throws<ArgumentException>(
            () => new Provider(
                resource,
                "provider",
                "Provider",
                "openai-compatible"));
    }

    [Fact]
    public void CapabilityKey_RejectsMissingAndOverlongValues()
    {
        Assert.Throws<ArgumentException>(
            () => new CapabilityKey(" "));

        Assert.Throws<ArgumentException>(
            () => new CapabilityKey(new string('x', 129)));

        var key = new CapabilityKey(new string('x', 128));
        Assert.Equal(128, key.Value.Length);
    }

    [Fact]
    public void ExecutionTarget_RequiresModelOrDeployment()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();

        Assert.Throws<ArgumentException>(
            () => new ExecutionTarget(
                CreateEnvelope(
                    ResourceKind.ExecutionTarget,
                    ExecutionTargetId.New(),
                    principal,
                    ResourceScope.Tenant(tenant)),
                ProviderId.New(),
                ProviderAccountId.New(),
                "target",
                "Target",
                new Uri("https://example.test/v1"),
                null,
                null,
                []));
    }

    [Fact]
    public void ExecutionTarget_RejectsNonHttpEndpoint()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();

        Assert.Throws<ArgumentException>(
            () => new ExecutionTarget(
                CreateEnvelope(
                    ResourceKind.ExecutionTarget,
                    ExecutionTargetId.New(),
                    principal,
                    ResourceScope.Tenant(tenant)),
                ProviderId.New(),
                ProviderAccountId.New(),
                "target",
                "Target",
                new Uri("ftp://example.test/model"),
                "model",
                null,
                []));
    }

    [Fact]
    public void ExecutionTarget_RejectsDuplicateCapabilityKeys()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var key = new CapabilityKey("text.generate");

        Assert.Throws<ArgumentException>(
            () => CreateTarget(
                principal,
                tenant,
                [
                    new CapabilityStateEntry(key, CapabilityState.Supported),
                    new CapabilityStateEntry(key, CapabilityState.Unknown)
                ]));
    }

    [Fact]
    public void ExecutionTarget_PreservesAllThreeCapabilityStates()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();

        var target = CreateTarget(
            principal,
            tenant,
            [
                new CapabilityStateEntry(
                    new CapabilityKey("text.generate"),
                    CapabilityState.Supported),
                new CapabilityStateEntry(
                    new CapabilityKey("vision"),
                    CapabilityState.Unsupported),
                new CapabilityStateEntry(
                    new CapabilityKey("structured.output"),
                    CapabilityState.Unknown)
            ]);

        Assert.Collection(
            target.Capabilities,
            item => Assert.Equal(CapabilityState.Supported, item.State),
            item => Assert.Equal(CapabilityState.Unsupported, item.State),
            item => Assert.Equal(CapabilityState.Unknown, item.State));
    }

    [Fact]
    public void ResourceAccessContext_RequiresMatchingOwnerAndScope()
    {
        var principal = PrincipalId.New();
        var otherPrincipal = PrincipalId.New();
        var tenant = TenantId.New();
        var otherTenant = TenantId.New();

        var resource = CreateEnvelope(
            ResourceKind.Provider,
            ProviderId.New(),
            principal,
            ResourceScope.Tenant(tenant));

        Assert.True(
            resource.Scope.Matches(
                new ResourceAccessContext(
                    DeploymentId.New(),
                    tenant,
                    principal)));

        Assert.False(
            resource.Scope.Matches(
                new ResourceAccessContext(
                    DeploymentId.New(),
                    otherTenant,
                    principal)));

        Assert.False(
            resource.Scope.Matches(
                new ResourceAccessContext(
                    DeploymentId.New(),
                    tenant,
                    otherPrincipal)));
    }

    private static Provider CreateProvider(
        ProviderId id,
        PrincipalId principal,
        TenantId tenant,
        string key = "provider",
        string displayName = "Provider") =>
        new(
            CreateEnvelope(
                ResourceKind.Provider,
                id,
                principal,
                ResourceScope.Tenant(tenant)),
            key,
            displayName,
            "openai-compatible");

    private static ExecutionTarget CreateTarget(
        PrincipalId principal,
        TenantId tenant,
        IReadOnlyList<CapabilityStateEntry> capabilities) =>
        new(
            CreateEnvelope(
                ResourceKind.ExecutionTarget,
                ExecutionTargetId.New(),
                principal,
                ResourceScope.Tenant(tenant)),
            ProviderId.New(),
            ProviderAccountId.New(),
            "target",
            "Target",
            new Uri("https://example.test/v1"),
            "example-model",
            null,
            capabilities);

    private static ResourceEnvelope<TIdentity> CreateEnvelope<TIdentity>(
        ResourceKind kind,
        TIdentity identity,
        PrincipalId principal,
        ResourceScope scope)
        where TIdentity : struct
    {
        var now = DateTimeOffset.UtcNow;

        return new ResourceEnvelope<TIdentity>(
            kind,
            identity,
            principal,
            scope,
            ResourceVersion.Initial,
            new ResourceProvenance(
                principal,
                now,
                CorrelationId.New()),
            ResourceLifecycle.Active(now));
    }
}
