using System.Collections.Concurrent;
using Hive.Core;

namespace Hive.Agents;

public enum UnderstandingGateStatus
{
    Blocked,
    Satisfied
}

public sealed record UnderstandingGatePolicy
{
    public UnderstandingGatePolicy(
        IReadOnlyCollection<string>? requiredInformation = null,
        bool confirmationRequired = false)
    {
        var values = requiredInformation?.ToArray() ?? [];
        var normalized = new HashSet<string>(StringComparer.Ordinal);

        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(
                    "Required information keys cannot be empty.",
                    nameof(requiredInformation));

            normalized.Add(value.Trim());
        }

        RequiredInformation = normalized
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

        ConfirmationRequired = confirmationRequired;
    }

    public IReadOnlyList<string> RequiredInformation { get; }

    public bool ConfirmationRequired { get; }
}

public sealed record UnderstandingGateResult(
    UnderstandingGateStatus Status,
    IReadOnlyList<string> MissingInformation,
    bool ConfirmationSatisfied)
{
    public bool IsSatisfied => Status == UnderstandingGateStatus.Satisfied;
}

public interface IUnderstandingGate
{
    UnderstandingGateResult Evaluate(
        UnderstandingGatePolicy policy,
        IReadOnlyCollection<string>? availableInformation,
        bool confirmationReceived);
}

public sealed class UnderstandingGate : IUnderstandingGate
{
    public UnderstandingGateResult Evaluate(
        UnderstandingGatePolicy policy,
        IReadOnlyCollection<string>? availableInformation,
        bool confirmationReceived)
    {
        ArgumentNullException.ThrowIfNull(policy);

        var available = new HashSet<string>(
            availableInformation ?? [],
            StringComparer.Ordinal);

        var missing = policy.RequiredInformation
            .Where(key => !available.Contains(key))
            .OrderBy(static key => key, StringComparer.Ordinal)
            .ToArray();

        var confirmationSatisfied =
            !policy.ConfirmationRequired || confirmationReceived;

        var status =
            missing.Length == 0 && confirmationSatisfied
                ? UnderstandingGateStatus.Satisfied
                : UnderstandingGateStatus.Blocked;

        return new UnderstandingGateResult(
            status,
            missing,
            confirmationSatisfied);
    }
}

public sealed record DelegationRequest
{
    private DelegationRequest(
        DelegationId id,
        AgentId requesterAgentId,
        RuntimeId requesterRuntimeId,
        AgentId delegateAgentId,
        RuntimeId delegateRuntimeId,
        string task,
        ResourceReference? source,
        ResourceProvenance provenance)
    {
        Id = id;
        RequesterAgentId = requesterAgentId;
        RequesterRuntimeId = requesterRuntimeId;
        DelegateAgentId = delegateAgentId;
        DelegateRuntimeId = delegateRuntimeId;
        Task = task;
        Source = source;
        Provenance = provenance;
    }

    public DelegationId Id { get; }

    public AgentId RequesterAgentId { get; }

    public RuntimeId RequesterRuntimeId { get; }

    public AgentId DelegateAgentId { get; }

    public RuntimeId DelegateRuntimeId { get; }

    public string Task { get; }

    public ResourceReference? Source { get; }

    public ResourceProvenance Provenance { get; }

    public static Result<DelegationRequest> Create(
        ResourceAccessContext requesterContext,
        AgentId requesterAgentId,
        RuntimeId requesterRuntimeId,
        AgentId delegateAgentId,
        RuntimeId delegateRuntimeId,
        string task,
        DateTimeOffset requestedAtUtc,
        ResourceReference? source = null,
        DelegationId? id = null)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            requesterContext,
            requesterAgentId,
            requesterRuntimeId);

        if (ownership.IsFailure)
            return Result<DelegationRequest>.Failure(ownership.Error!);

        if (delegateAgentId == default || delegateRuntimeId == default)
        {
            return Result<DelegationRequest>.Failure(
                Error.Validation(
                    "hive.agent.delegation.delegate-required",
                    "Delegate Agent and Runtime identities are required."));
        }

        if (requesterAgentId == delegateAgentId &&
            requesterRuntimeId == delegateRuntimeId)
        {
            return Result<DelegationRequest>.Failure(
                Error.Validation(
                    "hive.agent.delegation.same-runtime",
                    "Delegation must target a different RuntimeInstance."));
        }

        if (string.IsNullOrWhiteSpace(task))
        {
            return Result<DelegationRequest>.Failure(
                Error.Validation(
                    "hive.agent.delegation.task-required",
                    "Delegation task is required."));
        }

        var provenance = new ResourceProvenance(
            requesterContext.PrincipalId!.Value,
            requestedAtUtc,
            CorrelationId.New(),
            null,
            source);

        return Result<DelegationRequest>.Success(
            new DelegationRequest(
                id ?? DelegationId.New(),
                requesterAgentId,
                requesterRuntimeId,
                delegateAgentId,
                delegateRuntimeId,
                task.Trim(),
                source,
                provenance));
    }
}

public interface IDelegationChannel
{
    Result<DelegationRequest> Submit(
        ResourceAccessContext requesterContext,
        DelegationRequest request);

    Result<DelegationRequest> Read(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        DelegationId delegationId);
}

public sealed class InMemoryDelegationChannel : IDelegationChannel
{
    private readonly ConcurrentDictionary<DelegationId, DelegationRequest> _requests = new();

    public Result<DelegationRequest> Submit(
        ResourceAccessContext requesterContext,
        DelegationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ownership = RuntimeProtocolGuard.Validate(
            requesterContext,
            request.RequesterAgentId,
            request.RequesterRuntimeId);

        if (ownership.IsFailure)
            return Result<DelegationRequest>.Failure(ownership.Error!);

        if (request.Provenance.CreatedBy != requesterContext.PrincipalId!.Value)
        {
            return Result<DelegationRequest>.Failure(
                new Error(
                    "hive.agent.delegation.provenance-owner-mismatch",
                    ErrorCategory.Forbidden,
                    "Delegation provenance does not belong to the submitting principal."));
        }

        return _requests.TryAdd(request.Id, request)
            ? Result<DelegationRequest>.Success(request)
            : Result<DelegationRequest>.Failure(
                Error.Conflict(
                    "hive.agent.delegation.identity-conflict",
                    "The delegation identity is already in use."));
    }

    public Result<DelegationRequest> Read(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        DelegationId delegationId)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<DelegationRequest>.Failure(ownership.Error!);

        if (!_requests.TryGetValue(delegationId, out var request))
        {
            return Result<DelegationRequest>.Failure(
                new Error(
                    "hive.agent.delegation.not-found",
                    ErrorCategory.NotFound,
                    "The requested delegation does not exist."));
        }

        var isRequester =
            request.RequesterAgentId == agentId &&
            request.RequesterRuntimeId == runtimeId;

        var isDelegate =
            request.DelegateAgentId == agentId &&
            request.DelegateRuntimeId == runtimeId;

        if (!isRequester && !isDelegate)
        {
            return Result<DelegationRequest>.Failure(
                new Error(
                    "hive.agent.delegation.not-authorized",
                    ErrorCategory.Forbidden,
                    "Only the requesting or delegated RuntimeInstance may read the delegation."));
        }

        return Result<DelegationRequest>.Success(request);
    }
}
