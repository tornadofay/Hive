using System.Collections.Concurrent;
using Hive.Core;

namespace Hive.Agents;

public enum MemoryEvidenceKind
{
    Actual,
    Simulated
}

public sealed class AgentMemoryEntry
{
    private AgentMemoryEntry(
        ResourceEnvelope<MemoryId> resource,
        AgentId agentId,
        RuntimeId runtimeId,
        string key,
        string content,
        MemoryEvidenceKind evidenceKind)
    {
        Resource = resource;
        AgentId = agentId;
        RuntimeId = runtimeId;
        Key = key;
        Content = content;
        EvidenceKind = evidenceKind;
    }

    public ResourceEnvelope<MemoryId> Resource { get; }

    public MemoryId Id => Resource.Identity;

    public AgentId AgentId { get; }

    public RuntimeId RuntimeId { get; }

    public string Key { get; }

    public string Content { get; }

    public MemoryEvidenceKind EvidenceKind { get; }

    internal static AgentMemoryEntry Restore(
        ResourceEnvelope<MemoryId> resource,
        AgentId agentId,
        RuntimeId runtimeId,
        string key,
        string content,
        MemoryEvidenceKind evidenceKind) =>
        new(
            resource,
            agentId,
            runtimeId,
            key,
            content,
            evidenceKind);

    internal static Result<AgentMemoryEntry> Create(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key,
        string content,
        DateTimeOffset storedAtUtc,
        WorkItemBinding? workItemBinding = null,
        MemoryEvidenceKind evidenceKind = MemoryEvidenceKind.Actual)
    {
        if (!Enum.IsDefined(evidenceKind))
        {
            return Result<AgentMemoryEntry>.Failure(
                Error.Validation(
                    "hive.agent.memory.evidence-kind-invalid",
                    "Memory evidence kind is invalid."));
        }

        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<AgentMemoryEntry>.Failure(ownership.Error!);

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<AgentMemoryEntry>.Failure(
                Error.Validation(
                    "hive.agent.memory.key-required",
                    "Memory key is required."));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            return Result<AgentMemoryEntry>.Failure(
                Error.Validation(
                    "hive.agent.memory.content-required",
                    "Memory content is required."));
        }

        if (workItemBinding is not null &&
            (workItemBinding.AgentId != agentId ||
             workItemBinding.RuntimeId != runtimeId))
        {
            return Result<AgentMemoryEntry>.Failure(
                Error.Validation(
                    "hive.agent.memory.binding-mismatch",
                    "The WorkItem binding belongs to a different Agent or RuntimeInstance."));
        }

        var provenance = new ResourceProvenance(
            accessContext.PrincipalId!.Value,
            storedAtUtc,
            CorrelationId.New(),
            null,
            workItemBinding?.Provenance.Source);

        var resource = new ResourceEnvelope<MemoryId>(
            ResourceKind.Memory,
            MemoryId.New(),
            accessContext.PrincipalId.Value,
            ResourceScope.Runtime(runtimeId),
            ResourceVersion.Initial,
            provenance,
            ResourceLifecycle.Active(storedAtUtc));

        return Result<AgentMemoryEntry>.Success(
            new AgentMemoryEntry(
                resource,
                agentId,
                runtimeId,
                key.Trim(),
                content,
                evidenceKind));
    }
}

public interface IAgentMemoryStore
{
    Result<AgentMemoryEntry> Store(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key,
        string content,
        DateTimeOffset storedAtUtc,
        WorkItemBinding? workItemBinding = null,
        MemoryEvidenceKind evidenceKind = MemoryEvidenceKind.Actual);

    Result<IReadOnlyList<AgentMemoryEntry>> Retrieve(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key);

    Result<AgentMemoryEntry> Get(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        MemoryId memoryId);
}

public sealed class AgentMemoryStore : IAgentMemoryStore
{
    private readonly ConcurrentDictionary<MemoryId, AgentMemoryEntry> _entries = new();

    public Result<AgentMemoryEntry> Store(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key,
        string content,
        DateTimeOffset storedAtUtc,
        WorkItemBinding? workItemBinding = null,
        MemoryEvidenceKind evidenceKind = MemoryEvidenceKind.Actual)
    {
        var result = AgentMemoryEntry.Create(
            accessContext,
            agentId,
            runtimeId,
            key,
            content,
            storedAtUtc,
            workItemBinding,
            evidenceKind);

        if (result.IsFailure)
            return result;

        return _entries.TryAdd(result.Value!.Id, result.Value)
            ? result
            : Result<AgentMemoryEntry>.Failure(
                Error.Conflict(
                    "hive.agent.memory.identity-conflict",
                    "The memory identity is already in use."));
    }

    public Result<IReadOnlyList<AgentMemoryEntry>> Retrieve(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        string key)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<IReadOnlyList<AgentMemoryEntry>>.Failure(
                ownership.Error!);

        if (string.IsNullOrWhiteSpace(key))
        {
            return Result<IReadOnlyList<AgentMemoryEntry>>.Failure(
                Error.Validation(
                    "hive.agent.memory.key-required",
                    "Memory key is required."));
        }

        var normalized = key.Trim();

        var entries = _entries.Values
            .Where(entry =>
                entry.AgentId == agentId &&
                entry.RuntimeId == runtimeId &&
                string.Equals(
                    entry.Key,
                    normalized,
                    StringComparison.Ordinal))
            .OrderBy(entry => entry.Resource.Provenance.CreatedAtUtc)
            .ThenBy(entry => entry.Id.ToString(), StringComparer.Ordinal)
            .ToArray();

        return Result<IReadOnlyList<AgentMemoryEntry>>.Success(entries);
    }

    public Result<AgentMemoryEntry> Get(
        ResourceAccessContext accessContext,
        AgentId agentId,
        RuntimeId runtimeId,
        MemoryId memoryId)
    {
        var ownership = RuntimeProtocolGuard.Validate(
            accessContext,
            agentId,
            runtimeId);

        if (ownership.IsFailure)
            return Result<AgentMemoryEntry>.Failure(
                ownership.Error!);

        if (!_entries.TryGetValue(memoryId, out var entry))
        {
            return Result<AgentMemoryEntry>.Failure(
                new Error(
                    "hive.agent.memory.not-found",
                    ErrorCategory.NotFound,
                    "The requested memory entry does not exist in this RuntimeInstance."));
        }

        if (entry.AgentId != agentId || entry.RuntimeId != runtimeId)
        {
            return Result<AgentMemoryEntry>.Failure(
                new Error(
                    "hive.agent.memory.runtime-mismatch",
                    ErrorCategory.Forbidden,
                    "The requested memory entry belongs to another runtime boundary."));
        }

        return Result<AgentMemoryEntry>.Success(entry);
    }
}
