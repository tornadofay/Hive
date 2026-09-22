using System.Collections.ObjectModel;

namespace Hive.Core;

public enum CapabilityRequirementKind
{
    Required,
    Preferred,
    Optional,
    Forbidden
}

public enum ExecutionTargetSelectionMode
{
    Auto,
    Preferred,
    Fixed
}

public enum ExecutionTargetCostPolicy
{
    FreeOnly,
    FreePreferred,
    NoRestriction
}

public sealed record CapabilityRequirement
{
    public CapabilityRequirement(
        CapabilityKey capability,
        CapabilityRequirementKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Capability requirement kind is invalid.");
        }

        Capability = capability;
        Kind = kind;
    }

    public CapabilityKey Capability { get; }

    public CapabilityRequirementKind Kind { get; }
}

public sealed record ExecutionTargetSelectionRequest
{
    public ExecutionTargetSelectionRequest(
        IReadOnlyList<ExecutionTarget> targets,
        IReadOnlyList<CapabilityRequirement> requirements,
        ExecutionTargetSelectionMode mode = ExecutionTargetSelectionMode.Auto,
        ExecutionTargetId? preferredTargetId = null,
        ExecutionTargetId? fixedTargetId = null,
        ExecutionTargetCostPolicy costPolicy = ExecutionTargetCostPolicy.NoRestriction)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(requirements);

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Execution target selection mode is invalid.");
        }

        if (!Enum.IsDefined(costPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(costPolicy),
                costPolicy,
                "Execution target cost policy is invalid.");
        }

        if (targets.Count == 0)
            throw new ArgumentException("At least one execution target is required.", nameof(targets));

        var targetIds = new HashSet<ExecutionTargetId>();

        foreach (var target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);

            if (!targetIds.Add(target.Id))
            {
                throw new ArgumentException(
                    $"Duplicate execution target '{target.Id}' is not allowed.",
                    nameof(targets));
            }
        }

        var requirementKeys = new HashSet<(CapabilityKey Capability, CapabilityRequirementKind Kind)>();

        foreach (var requirement in requirements)
        {
            ArgumentNullException.ThrowIfNull(requirement);

            if (!requirementKeys.Add((requirement.Capability, requirement.Kind)))
            {
                throw new ArgumentException(
                    $"Duplicate capability requirement '{requirement.Capability}' with kind '{requirement.Kind}' is not allowed.",
                    nameof(requirements));
            }
        }

        var capabilityKinds = requirements
            .GroupBy(requirement => requirement.Capability)
            .Where(group => group.Count() > 1)
            .FirstOrDefault();

        if (capabilityKinds is not null &&
            capabilityKinds.Any(requirement => requirement.Kind is CapabilityRequirementKind.Required or CapabilityRequirementKind.Forbidden))
        {
            throw new ArgumentException(
                $"Capability '{capabilityKinds.Key}' cannot have multiple conflicting requirement kinds.",
                nameof(requirements));
        }

        switch (mode)
        {
            case ExecutionTargetSelectionMode.Auto
                when preferredTargetId is not null || fixedTargetId is not null:
                throw new ArgumentException(
                    "Auto selection cannot specify a preferred or fixed target.",
                    nameof(mode));

            case ExecutionTargetSelectionMode.Preferred
                when fixedTargetId is not null:
                throw new ArgumentException(
                    "Preferred selection cannot specify a fixed target.",
                    nameof(fixedTargetId));

            case ExecutionTargetSelectionMode.Fixed
                when preferredTargetId is not null:
                throw new ArgumentException(
                    "Fixed selection cannot specify a preferred target.",
                    nameof(preferredTargetId));

            case ExecutionTargetSelectionMode.Preferred
                when preferredTargetId is null:
                throw new ArgumentException(
                    "Preferred selection requires a preferred target identity.",
                    nameof(preferredTargetId));

            case ExecutionTargetSelectionMode.Fixed
                when fixedTargetId is null:
                throw new ArgumentException(
                    "Fixed selection requires a fixed target identity.",
                    nameof(fixedTargetId));
        }

        Targets = new ReadOnlyCollection<ExecutionTarget>(
            targets.ToList());

        Requirements = new ReadOnlyCollection<CapabilityRequirement>(
            requirements.ToList());

        Mode = mode;
        PreferredTargetId = preferredTargetId;
        FixedTargetId = fixedTargetId;
        CostPolicy = costPolicy;
    }

    public IReadOnlyList<ExecutionTarget> Targets { get; }

    public IReadOnlyList<CapabilityRequirement> Requirements { get; }

    public ExecutionTargetSelectionMode Mode { get; }

    public ExecutionTargetId? PreferredTargetId { get; }

    public ExecutionTargetId? FixedTargetId { get; }

    public ExecutionTargetCostPolicy CostPolicy { get; }
}

public enum ExecutionTargetSelectionDiagnosticStatus
{
    Qualified,
    Rejected
}

public sealed record ExecutionTargetSelectionDiagnostic
{
    public ExecutionTargetSelectionDiagnostic(
        ExecutionTarget target,
        ExecutionTargetSelectionDiagnosticStatus status,
        int score,
        IReadOnlyList<string> reasons)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(reasons);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Selection diagnostic status is invalid.");
        }

        TargetId = target.Id;
        TargetKey = target.Key;
        TargetDisplayName = target.DisplayName;
        Status = status;
        Score = score;
        Reasons = new ReadOnlyCollection<string>(
            reasons.Select(reason =>
            {
                if (string.IsNullOrWhiteSpace(reason))
                    throw new ArgumentException(
                        "Diagnostic reasons cannot contain empty values.",
                        nameof(reasons));

                return reason.Trim();
            }).ToList());
    }

    public ExecutionTargetId TargetId { get; }

    public string TargetKey { get; }

    public string TargetDisplayName { get; }

    public ExecutionTargetSelectionDiagnosticStatus Status { get; }

    public int Score { get; }

    public IReadOnlyList<string> Reasons { get; }
}

public sealed record ExecutionTargetSelectionResult
{
    public ExecutionTargetSelectionResult(
        ExecutionTarget selectedTarget,
        IReadOnlyList<ExecutionTargetSelectionDiagnostic> diagnostics,
        ExecutionTargetSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(selectedTarget);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(request);

        SelectedTarget = selectedTarget;
        Diagnostics = new ReadOnlyCollection<ExecutionTargetSelectionDiagnostic>(
            diagnostics.ToList());
        Mode = request.Mode;
        CostPolicy = request.CostPolicy;
    }

    public ExecutionTarget SelectedTarget { get; }

    public IReadOnlyList<ExecutionTargetSelectionDiagnostic> Diagnostics { get; }

    public ExecutionTargetSelectionMode Mode { get; }

    public ExecutionTargetCostPolicy CostPolicy { get; }
}

public static class ExecutionTargetSelector
{
    public static Result<ExecutionTargetSelectionResult> Select(
        ExecutionTargetSelectionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var evaluations = request.Targets
            .Select(target => Evaluate(target, request.Requirements))
            .ToList();

        var diagnostics = evaluations
            .Select(evaluation => evaluation.Diagnostic)
            .ToList();

        var qualifying = evaluations
            .Where(evaluation => evaluation.Qualifies)
            .ToList();

        if (request.Mode == ExecutionTargetSelectionMode.Fixed)
        {
            var fixedEvaluation = evaluations.FirstOrDefault(
                evaluation => evaluation.Target.Id == request.FixedTargetId);

            if (fixedEvaluation is null)
            {
                return Result<ExecutionTargetSelectionResult>.Failure(
                    Error(
                        "hive.execution-target.selection.fixed-target-not-found",
                        ErrorCategory.NotFound,
                        $"Fixed execution target '{request.FixedTargetId}' was not found among the supplied targets."));
            }

            if (!fixedEvaluation.Qualifies)
            {
                return Result<ExecutionTargetSelectionResult>.Failure(
                    Error(
                        "hive.execution-target.selection.fixed-target-rejected",
                        ErrorCategory.Unsupported,
                        $"Fixed execution target '{fixedEvaluation.Target.Key}' does not satisfy the required capability policy."));
            }

            return Result<ExecutionTargetSelectionResult>.Success(
                new ExecutionTargetSelectionResult(
                    fixedEvaluation.Target,
                    diagnostics,
                    request));
        }

        if (qualifying.Count == 0)
        {
            return Result<ExecutionTargetSelectionResult>.Failure(
                Error(
                    "hive.execution-target.selection.no-qualifying-target",
                    ErrorCategory.Unsupported,
                    "No execution target satisfies the required capability policy."));
        }

        ExecutionTargetEvaluation selected;

        if (request.Mode == ExecutionTargetSelectionMode.Preferred)
        {
            var preferred = qualifying.FirstOrDefault(
                evaluation => evaluation.Target.Id == request.PreferredTargetId);

            selected = preferred ?? SelectBest(qualifying);
        }
        else
        {
            selected = SelectBest(qualifying);
        }

        return Result<ExecutionTargetSelectionResult>.Success(
            new ExecutionTargetSelectionResult(
                selected.Target,
                diagnostics,
                request));
    }

    private static ExecutionTargetEvaluation Evaluate(
        ExecutionTarget target,
        IReadOnlyList<CapabilityRequirement> requirements)
    {
        var states = target.Capabilities.ToDictionary(
            capability => capability.Capability,
            capability => capability.State);

        var reasons = new List<string>();
        var qualifies = true;
        var score = 0;

        foreach (var requirement in requirements)
        {
            var state = states.TryGetValue(
                requirement.Capability,
                out var configuredState)
                ? configuredState
                : CapabilityState.Unknown;

            switch (requirement.Kind)
            {
                case CapabilityRequirementKind.Required:
                    if (state == CapabilityState.Supported)
                    {
                        reasons.Add(
                            $"Required '{requirement.Capability}' is supported.");
                        score += 1000;
                    }
                    else
                    {
                        qualifies = false;
                        reasons.Add(
                            $"Required '{requirement.Capability}' is {state.ToString().ToLowerInvariant()}.");
                    }

                    break;

                case CapabilityRequirementKind.Forbidden:
                    if (state == CapabilityState.Unsupported)
                    {
                        reasons.Add(
                            $"Forbidden '{requirement.Capability}' is explicitly unsupported.");
                    }
                    else
                    {
                        qualifies = false;
                        reasons.Add(
                            $"Forbidden '{requirement.Capability}' is {state.ToString().ToLowerInvariant()}.");
                    }

                    break;

                case CapabilityRequirementKind.Preferred:
                    if (state == CapabilityState.Supported)
                    {
                        reasons.Add(
                            $"Preferred '{requirement.Capability}' is supported.");
                        score += 100;
                    }
                    else if (state == CapabilityState.Unknown)
                    {
                        reasons.Add(
                            $"Preferred '{requirement.Capability}' is unknown.");
                        score += 10;
                    }
                    else
                    {
                        reasons.Add(
                            $"Preferred '{requirement.Capability}' is unsupported.");
                    }

                    break;

                case CapabilityRequirementKind.Optional:
                    if (state == CapabilityState.Supported)
                    {
                        reasons.Add(
                            $"Optional '{requirement.Capability}' is supported.");
                        score += 10;
                    }
                    else
                    {
                        reasons.Add(
                            $"Optional '{requirement.Capability}' is {state.ToString().ToLowerInvariant()}.");
                    }

                    break;

                default:
                    throw new InvalidOperationException(
                        $"Unsupported capability requirement kind '{requirement.Kind}'.");
            }
        }

        if (requirements.Count == 0)
            reasons.Add("No capability requirements were supplied.");

        return new ExecutionTargetEvaluation(
            target,
            qualifies,
            score,
            new ExecutionTargetSelectionDiagnostic(
                target,
                qualifies
                    ? ExecutionTargetSelectionDiagnosticStatus.Qualified
                    : ExecutionTargetSelectionDiagnosticStatus.Rejected,
                score,
                reasons));
    }

    private static ExecutionTargetEvaluation SelectBest(
        IReadOnlyList<ExecutionTargetEvaluation> candidates) =>
        candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Target.Key, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.Target.Id.Value)
            .First();

    private static Error Error(
        string code,
        ErrorCategory category,
        string message) =>
        new(code, category, message);

    private sealed record ExecutionTargetEvaluation(
        ExecutionTarget Target,
        bool Qualifies,
        int Score,
        ExecutionTargetSelectionDiagnostic Diagnostic);
}
