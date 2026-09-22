using Hive.Core;
using Hive.Host.WinForms.UI.Controls;

namespace Hive.Example.WinForms;

internal sealed class ExecutionTargetSelectionExampleView : UserControl
{
    private readonly HiveExampleTestSurface _surface;
    private readonly IHiveExampleOutput _output;

    public ExecutionTargetSelectionExampleView(IHiveExampleOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        _output = output;

        Dock = DockStyle.Fill;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        _surface = new HiveExampleTestSurface
        {
            Dock = DockStyle.Fill,
            RunButtonText = "Run selection"
        };

        _surface.SetInformation(
            "Applies capability requirements and Auto / Preferred / Fixed selection modes to a local set of ExecutionTarget contracts. No provider transport or network call is performed.",
            "Auto selects the highest-scoring qualifying target, Preferred uses the preferred target when it qualifies and otherwise falls back, and Fixed never falls back. Diagnostics explain both accepted and rejected targets.",
            "Selection policy",
            "Required / Preferred / Optional / Forbidden capabilities");

        _surface.CodeSnippet = """
            var result = ExecutionTargetSelector.Select(
                new ExecutionTargetSelectionRequest(
                    targets,
                    requirements,
                    ExecutionTargetSelectionMode.Auto));

            if (result.IsSuccess)
                Console.WriteLine(result.Value.SelectedTarget.Key);
            """;

        _surface.ConfigureRun(
            RunExampleAsync,
            _output,
            FindForm());

        Controls.Add(_surface);

        if (FindForm() is HiveForm form)
            form.ThemeManager.Apply(this);
    }

    private Task RunExampleAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var targets = new[]
        {
            CreateTarget(
                "text-target",
                [
                    Capability("text.generate", CapabilityState.Supported),
                    Capability("vision", CapabilityState.Unsupported),
                    Capability("structured.output", CapabilityState.Unsupported)
                ]),
            CreateTarget(
                "vision-target",
                [
                    Capability("text.generate", CapabilityState.Supported),
                    Capability("vision", CapabilityState.Supported),
                    Capability("structured.output", CapabilityState.Unsupported)
                ]),
            CreateTarget(
                "unknown-vision-target",
                [
                    Capability("text.generate", CapabilityState.Supported)
                ]),
            CreateTarget(
                "structured-target",
                [
                    Capability("text.generate", CapabilityState.Supported),
                    Capability("vision", CapabilityState.Supported),
                    Capability("structured.output", CapabilityState.Supported)
                ])
        };

        var requirements = new[]
        {
            Requirement("text.generate", CapabilityRequirementKind.Required),
            Requirement("vision", CapabilityRequirementKind.Preferred),
            Requirement("structured.output", CapabilityRequirementKind.Forbidden)
        };

        var autoResult = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                targets,
                requirements,
                ExecutionTargetSelectionMode.Auto,
                costPolicy: ExecutionTargetCostPolicy.FreePreferred));

        EnsureSuccess(autoResult, "Auto selection");

        var preferredResult = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                targets,
                requirements,
                ExecutionTargetSelectionMode.Preferred,
                preferredTargetId: targets[0].Id));

        EnsureSuccess(preferredResult, "Preferred selection");

        var fixedResult = ExecutionTargetSelector.Select(
            new ExecutionTargetSelectionRequest(
                targets,
                [
                    Requirement("text.generate", CapabilityRequirementKind.Required),
                    Requirement("vision", CapabilityRequirementKind.Required)
                ],
                ExecutionTargetSelectionMode.Fixed,
                fixedTargetId: targets[2].Id));

        if (fixedResult.IsSuccess)
        {
            throw new InvalidOperationException(
                "Fixed selection unexpectedly accepted the unknown vision target.");
        }

        _output.Write(
            "Capability-aware Execution Target Selection",
            $"""
            Requirements:
              text.generate = Required
              vision = Preferred
              structured.output = Forbidden

            Auto selected: {autoResult.Value!.SelectedTarget.Key}
            Preferred selected: {preferredResult.Value!.SelectedTarget.Key}
            Fixed unknown-vision result: {fixedResult.Error!.Code}
            Cost policy retained separately: {autoResult.Value.CostPolicy}

            Diagnostics:
            {FormatDiagnostics(autoResult.Value.Diagnostics)}
            """);

        return Task.CompletedTask;
    }

    private static string FormatDiagnostics(
        IReadOnlyList<ExecutionTargetSelectionDiagnostic> diagnostics) =>
        string.Join(
            Environment.NewLine,
            diagnostics.Select(
                diagnostic =>
                    $"- {diagnostic.TargetKey}: {diagnostic.Status}; score={diagnostic.Score}; {string.Join(" ", diagnostic.Reasons)}"));

    private static ExecutionTarget CreateTarget(
        string key,
        IReadOnlyList<CapabilityStateEntry> capabilities)
    {
        var principal = PrincipalId.New();
        var tenant = TenantId.New();
        var now = DateTimeOffset.UtcNow;

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
            key,
            key,
            new Uri($"https://{key}.example.test/v1"),
            "example-model",
            null,
            capabilities);
    }

    private static CapabilityStateEntry Capability(
        string key,
        CapabilityState state) =>
        new(new CapabilityKey(key), state);

    private static CapabilityRequirement Requirement(
        string key,
        CapabilityRequirementKind kind) =>
        new(new CapabilityKey(key), kind);

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