using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class ExecutionTargetFavoriteFilterTests
{
    [Fact]
    public void EmptyFavorites_ReturnsSuppliedCandidatesUnchanged()
    {
        var targets = new[]
        {
            CreateTarget(),
            CreateTarget()
        };

        var result = ExecutionTargetFavoriteFilter.Apply(
            targets,
            Array.Empty<ExecutionTargetId>());

        Assert.Same(targets, result);
    }

    [Fact]
    public void NonEmptyFavorites_RestrictsCandidatesByDurableIdentityAndPreservesOrder()
    {
        var first = CreateTarget();
        var second = CreateTarget();
        var third = CreateTarget();

        var result = ExecutionTargetFavoriteFilter.Apply(
            [first, second, third],
            [third.Id, first.Id]);

        Assert.Equal(
            [first.Id, third.Id],
            result.Select(target => target.Id));
        Assert.Equal(
            [first.Model, third.Model],
            result.Select(target => target.Model));
    }

    [Fact]
    public void FavoriteFilter_DoesNotMutateTargetsOrCapabilities()
    {
        var target = CreateTarget();
        var originalCapabilities = target.Capabilities.ToArray();

        var result = ExecutionTargetFavoriteFilter.Apply(
            [target],
            [target.Id]);

        Assert.Single(result);
        Assert.Equal(originalCapabilities, target.Capabilities);
    }

    private static ExecutionTarget CreateTarget()
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

        return new ExecutionTarget(
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
            ProviderId.New(),
            ProviderAccountId.New(),
            "target",
            "Example Target",
            new Uri("https://example.test/v1"),
            "model",
            null,
            [
                new CapabilityStateEntry(
                    HiveCapabilityKeys.TextGeneration,
                    CapabilityState.Supported)
            ]);
    }
}
