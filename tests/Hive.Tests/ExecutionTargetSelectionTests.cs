using Hive.Core;
using Xunit;

namespace Hive.Tests;

public sealed class ExecutionTargetSelectionTests
{
    [Fact]
    public void Select_AutoChoosesTargetWithPreferredSupportedCapability()
    {
        var targets = new[]
        {
            CreateTarget("text-only", [
                Capability("text.generate", CapabilityState.Supported)
            ]),
            CreateTarget("vision", [
                Capability("text.generate", CapabilityState.Supported),
                Capability("vision", CapabilityState.Supported)
            ])
        };

        var request = new ExecutionTargetSelectionRequest(
            targets,
            [
                Requirement("text.generate", CapabilityRequirementKind.Required),
                Requirement("vision", CapabilityRequirementKind.Preferred)
            ]);

        var result = ExecutionTargetSelector.Select(request);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("vision", result.Value!.SelectedTarget.Key);
        Assert.All(
            result.Value.Diagnostics,
            diagnostic => Assert.NotEmpty(diagnostic.Reasons));
    }

    [Fact]
    public void Select_ExcludesUnsupportedRequiredCapability()
    {
        var targets = new[]
        {
            CreateTarget("unsupported", [
                Capability("text.generate", CapabilityState.Unsupported)
            ]),
            CreateTarget("supported", [
                Capability("text.generate", CapabilityState.Supported)
            ])
        };

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                targets,
                [
                    Requirement("text.generate", CapabilityRequirementKind.Required)
                ]));

        Assert.True(result.IsSuccess);
        Assert.Equal("supported", result.Value!.SelectedTarget.Key);

        var rejected = Assert.Single(
            result.Value.Diagnostics,
            diagnostic => diagnostic.TargetKey == "unsupported");

        Assert.Equal(
            ExecutionTargetSelectionDiagnosticStatus.Rejected,
            rejected.Status);
    }

    [Fact]
    public void Select_ExcludesUnknownRequiredCapability()
    {
        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [CreateTarget("unknown", [])],
                [Requirement("vision", CapabilityRequirementKind.Required)]));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.execution-target.selection.no-qualifying-target",
            result.Error!.Code);
    }

    [Fact]
    public void Select_ForbiddenCapabilityRequiresExplicitlyUnsupportedState()
    {
        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [
                    CreateTarget("unknown", []),
                    CreateTarget("clean", [
                        Capability("vision", CapabilityState.Unsupported)
                    ])
                ],
                [Requirement("vision", CapabilityRequirementKind.Forbidden)]));

        Assert.True(result.IsSuccess);
        Assert.Equal("clean", result.Value!.SelectedTarget.Key);

        var unknown = Assert.Single(
            result.Value.Diagnostics,
            diagnostic => diagnostic.TargetKey == "unknown");

        Assert.Equal(
            ExecutionTargetSelectionDiagnosticStatus.Rejected,
            unknown.Status);
    }

    [Fact]
    public void Select_OptionalCapabilityDoesNotRejectTarget()
    {
        var targets = new[]
        {
            CreateTarget("without-optional", []),
            CreateTarget("with-optional", [
                Capability("structured.output", CapabilityState.Supported)
            ])
        };

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                targets,
                [Requirement(
                    "structured.output",
                    CapabilityRequirementKind.Optional)]));

        Assert.True(result.IsSuccess);
        Assert.Equal("with-optional", result.Value!.SelectedTarget.Key);
    }

    [Fact]
    public void Select_PreferredModeUsesPreferredTargetWhenItQualifies()
    {
        var preferred = CreateTarget("preferred", [
            Capability("text.generate", CapabilityState.Supported)
        ]);

        var strongerAlternative = CreateTarget("stronger", [
            Capability("text.generate", CapabilityState.Supported),
            Capability("vision", CapabilityState.Supported)
        ]);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [preferred, strongerAlternative],
                [
                    Requirement("text.generate", CapabilityRequirementKind.Required),
                    Requirement("vision", CapabilityRequirementKind.Preferred)
                ],
                ExecutionTargetSelectionMode.Preferred,
                preferredTargetId: preferred.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("preferred", result.Value!.SelectedTarget.Key);
    }

    [Fact]
    public void Select_PreferredModeFallsBackWhenPreferredTargetIsRejected()
    {
        var preferred = CreateTarget("preferred", []);
        var fallback = CreateTarget("fallback", [
            Capability("vision", CapabilityState.Supported)
        ]);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [preferred, fallback],
                [Requirement("vision", CapabilityRequirementKind.Required)],
                ExecutionTargetSelectionMode.Preferred,
                preferredTargetId: preferred.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal("fallback", result.Value!.SelectedTarget.Key);
    }

    [Fact]
    public void Select_FixedModeNeverFallsBack()
    {
        var fixedTarget = CreateTarget("fixed", []);
        var alternative = CreateTarget("alternative", [
            Capability("vision", CapabilityState.Supported)
        ]);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [fixedTarget, alternative],
                [Requirement("vision", CapabilityRequirementKind.Required)],
                ExecutionTargetSelectionMode.Fixed,
                fixedTargetId: fixedTarget.Id));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.execution-target.selection.fixed-target-rejected",
            result.Error!.Code);
    }

    [Fact]
    public void Select_ReturnsNotFoundForUnknownFixedTarget()
    {
        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [CreateTarget("available", [
                    Capability("text.generate", CapabilityState.Supported)
                ])],
                [],
                ExecutionTargetSelectionMode.Fixed,
                fixedTargetId: ExecutionTargetId.New()));

        Assert.True(result.IsFailure);
        Assert.Equal(
            "hive.execution-target.selection.fixed-target-not-found",
            result.Error!.Code);
    }

    [Fact]
    public void Contracts_RejectInvalidSelectionModeArguments()
    {
        var target = CreateTarget("target", []);

        Assert.Throws<ArgumentException>(
            () => new ExecutionTargetSelectionRequest(
                [target],
                [],
                ExecutionTargetSelectionMode.Preferred));

        Assert.Throws<ArgumentException>(
            () => new ExecutionTargetSelectionRequest(
                [target],
                [],
                ExecutionTargetSelectionMode.Fixed));

        Assert.Throws<ArgumentException>(
            () => new ExecutionTargetSelectionRequest(
                [target],
                [],
                ExecutionTargetSelectionMode.Auto,
                preferredTargetId: target.Id));
    }

    [Fact]
    public void Contracts_RejectDuplicateTargetsAndConflictingRequirements()
    {
        var target = CreateTarget("target", []);

        Assert.Throws<ArgumentException>(
            () => new ExecutionTargetSelectionRequest(
                [target, target],
                []));

        Assert.Throws<ArgumentException>(
            () => new ExecutionTargetSelectionRequest(
                [target],
                [
                    Requirement("vision", CapabilityRequirementKind.Required),
                    Requirement("vision", CapabilityRequirementKind.Optional)
                ]));
    }

    [Fact]
    public void Select_IsDeterministicForEqualScores()
    {
        var first = CreateTarget("alpha", []);
        var second = CreateTarget("beta", []);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [second, first],
                []));

        Assert.True(result.IsSuccess);
        Assert.Equal("alpha", result.Value!.SelectedTarget.Key);
    }



    [Fact]
    public void Select_RejectsRetiredTargets()
    {
        var retired = CreateTarget("retired", [
            Capability("text.generate", CapabilityState.Supported)
        ]);

        retired = new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                retired.Resource.Kind,
                retired.Id,
                retired.Resource.Owner,
                retired.Resource.Scope,
                retired.Resource.Version,
                retired.Resource.Provenance,
                retired.Resource.Lifecycle.TransitionTo(
                    ResourceLifecycleStatus.Retired,
                    DateTimeOffset.UtcNow),
                retired.Resource.Metadata),
            retired.ProviderId,
            retired.ProviderAccountId,
            retired.Key,
            retired.DisplayName,
            retired.Endpoint,
            retired.Model,
            retired.Deployment,
            retired.Capabilities);

        var active = CreateTarget("active", [
            Capability("text.generate", CapabilityState.Supported)
        ]);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [retired, active],
                [Requirement("text.generate", CapabilityRequirementKind.Required)]));

        Assert.True(result.IsSuccess);
        Assert.Equal("active", result.Value!.SelectedTarget.Key);

        var retiredDiagnostic = Assert.Single(
            result.Value.Diagnostics,
            diagnostic => diagnostic.TargetKey == "retired");

        Assert.Equal(
            ExecutionTargetSelectionDiagnosticStatus.Rejected,
            retiredDiagnostic.Status);
        Assert.Contains(
            "lifecycle is retired",
            string.Join(" ", retiredDiagnostic.Reasons),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Select_DoesNotExposeEndpointOrAccountInDiagnostics()
    {
        var target = CreateTarget("safe-target", []);

        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [target],
                []));

        Assert.True(result.IsSuccess);

        var diagnostic = Assert.Single(result.Value!.Diagnostics);
        Assert.DoesNotContain(
            target.Endpoint.ToString(),
            string.Join(" ", diagnostic.Reasons),
            StringComparison.Ordinal);
        Assert.Equal(target.Id, diagnostic.TargetId);
        Assert.Equal(target.Key, diagnostic.TargetKey);
    }

    [Fact]
    public void CostPolicy_IsRetainedSeparatelyFromCapabilityRequirements()
    {
        var result = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                [CreateTarget("target", [
                    Capability("text.generate", CapabilityState.Supported)
                ])],
                [Requirement(
                    "text.generate",
                    CapabilityRequirementKind.Required)],
                costPolicy: ExecutionTargetCostPolicy.FreePreferred));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ExecutionTargetCostPolicy.FreePreferred,
            result.Value!.CostPolicy);
    }

    private static CapabilityStateEntry Capability(
        string key,
        CapabilityState state) =>
        new(new CapabilityKey(key), state);

    private static CapabilityRequirement Requirement(
        string key,
        CapabilityRequirementKind kind) =>
        new(new CapabilityKey(key), kind);

    private static ExecutionTarget CreateTarget(
        string key,
        IReadOnlyList<CapabilityStateEntry> capabilities)
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

        var providerId = ProviderId.New();
        var accountId = ProviderAccountId.New();
        var targetId = ExecutionTargetId.New();

        return new ExecutionTarget(
            new ResourceEnvelope<ExecutionTargetId>(
                ResourceKind.ExecutionTarget,
                targetId,
                principal,
                ResourceScope.Tenant(tenant),
                ResourceVersion.Initial,
                new ResourceProvenance(
                    principal,
                    now,
                    CorrelationId.New()),
                ResourceLifecycle.Active(now)),
            providerId,
            accountId,
            key,
            key,
            new Uri($"https://{key}.example.test/v1"),
            "example-model",
            null,
            capabilities);
    }
}
