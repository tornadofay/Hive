using System.Collections.ObjectModel;

namespace Hive.Core;

public enum ResourceScopeKind
{
    Global,
    Tenant,
    User,
    Workspace,
    Agent,
    Runtime,
    Execution
}

public readonly record struct ResourceScope
{
    private ResourceScope(ResourceScopeKind kind, Guid? identity)
    {
        Kind = kind;
        Identity = identity;
    }

    public ResourceScopeKind Kind { get; }

    public Guid? Identity { get; }

    public static ResourceScope Global() => new(ResourceScopeKind.Global, null);

    public static ResourceScope Tenant(TenantId tenantId) =>
        new(ResourceScopeKind.Tenant, tenantId.Value);

    public static ResourceScope User(UserId userId) =>
        new(ResourceScopeKind.User, userId.Value);

    public static ResourceScope Workspace(WorkspaceId workspaceId) =>
        new(ResourceScopeKind.Workspace, workspaceId.Value);

    public static ResourceScope Agent(AgentId agentId) =>
        new(ResourceScopeKind.Agent, agentId.Value);

    public static ResourceScope Runtime(RuntimeId runtimeId) =>
        new(ResourceScopeKind.Runtime, runtimeId.Value);

    public static ResourceScope Execution(ExecutionId executionId) =>
        new(ResourceScopeKind.Execution, executionId.Value);

    public bool Matches(ResourceAccessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.PrincipalId is null || context.DeploymentId is null)
            return false;

        return Kind switch
        {
            ResourceScopeKind.Global => true,

            ResourceScopeKind.Tenant =>
                context.TenantId is not null
                && Identity == context.TenantId.Value.Value,

            ResourceScopeKind.User =>
                context.TenantId is not null
                && context.UserId is not null
                && Identity == context.UserId.Value.Value,

            ResourceScopeKind.Workspace =>
                context.TenantId is not null
                && context.WorkspaceId is not null
                && Identity == context.WorkspaceId.Value.Value,

            ResourceScopeKind.Agent =>
                context.TenantId is not null
                && context.AgentId is not null
                && Identity == context.AgentId.Value.Value,

            ResourceScopeKind.Runtime =>
                context.TenantId is not null
                && context.AgentId is not null
                && context.RuntimeId is not null
                && Identity == context.RuntimeId.Value.Value,

            ResourceScopeKind.Execution =>
                context.TenantId is not null
                && context.AgentId is not null
                && context.RuntimeId is not null
                && context.ExecutionId is not null
                && Identity == context.ExecutionId.Value.Value,

            _ => false
        };
    }
}

public sealed record ResourceAccessContext(
    DeploymentId? DeploymentId = null,
    TenantId? TenantId = null,
    PrincipalId? PrincipalId = null,
    UserId? UserId = null,
    SessionId? SessionId = null,
    WorkspaceId? WorkspaceId = null,
    AgentId? AgentId = null,
    HiveId? HiveId = null,
    RuntimeId? RuntimeId = null,
    ExecutionId? ExecutionId = null);

public readonly record struct ResourceVersion
{
    public ResourceVersion(long value)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Resource version must be greater than zero.");

        Value = value;
    }

    public long Value { get; }

    public static ResourceVersion Initial => new(1);

    public ResourceVersion Next() =>
        Value == long.MaxValue
            ? throw new InvalidOperationException("Resource version limit reached.")
            : new(Value + 1);

    public override string ToString() => Value.ToString();
}

public enum ResourceLifecycleStatus
{
    Active,
    Suspended,
    Retired
}

public sealed record ResourceLifecycle
{
    public ResourceLifecycle(ResourceLifecycleStatus status, DateTimeOffset changedAtUtc)
    {
        Status = status;
        ChangedAtUtc = changedAtUtc.ToUniversalTime();
    }

    public ResourceLifecycleStatus Status { get; }

    public DateTimeOffset ChangedAtUtc { get; }

    public static ResourceLifecycle Active(DateTimeOffset changedAtUtc) =>
        new(ResourceLifecycleStatus.Active, changedAtUtc);

    public ResourceLifecycle TransitionTo(ResourceLifecycleStatus next, DateTimeOffset changedAtUtc)
    {
        if (Status == ResourceLifecycleStatus.Retired && next != ResourceLifecycleStatus.Retired)
        {
            throw new InvalidOperationException("A retired resource cannot become active or suspended.");
        }

        return new(next, changedAtUtc);
    }
}

public sealed record ResourceProvenance
{
    public ResourceProvenance(
        PrincipalId createdBy,
        DateTimeOffset createdAtUtc,
        CorrelationId correlationId,
        CausationId? causationId = null,
        ResourceReference? source = null)
    {
        if (createdBy == default)
            throw new ArgumentException("Creating principal is required.", nameof(createdBy));

        if (correlationId == default)
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        CorrelationId = correlationId;
        CausationId = causationId;
        Source = source;
    }

    public PrincipalId CreatedBy { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public CorrelationId CorrelationId { get; }

    public CausationId? CausationId { get; }

    public ResourceReference? Source { get; }
}

public readonly record struct ResourceReference
{
    public ResourceReference(ResourceKind kind, Guid identity)
    {
        if (identity == Guid.Empty)
            throw new ArgumentException("Resource reference identity cannot be empty.", nameof(identity));

        Kind = kind;
        Identity = identity;
    }

    public ResourceKind Kind { get; }

    public Guid Identity { get; }
}

public sealed class ResourceIdentitySnapshot<TIdentity>
    where TIdentity : struct
{
    public ResourceIdentitySnapshot(
        ResourceKind kind,
        TIdentity identity,
        ResourceVersion version)
    {
        Kind = kind;
        Identity = identity;
        Version = version;
    }

    public ResourceKind Kind { get; }

    public TIdentity Identity { get; }

    public ResourceVersion Version { get; }
}

public sealed class ResourceEnvelope<TIdentity>
    where TIdentity : struct
{
    public ResourceEnvelope(
        ResourceKind kind,
        TIdentity identity,
        PrincipalId owner,
        ResourceScope scope,
        ResourceVersion version,
        ResourceProvenance provenance,
        ResourceLifecycle lifecycle,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (!Enum.IsDefined(kind))
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Resource kind is invalid.");

        if (EqualityComparer<TIdentity>.Default.Equals(identity, default))
            throw new ArgumentException("Resource identity is required.", nameof(identity));

        if (owner == default)
            throw new ArgumentException("Resource owner is required.", nameof(owner));

        Kind = kind;
        Identity = identity;
        Owner = owner;
        Scope = scope;
        Version = version;
        Provenance = provenance ?? throw new ArgumentNullException(nameof(provenance));
        Lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));

        var copy = metadata is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(metadata, StringComparer.Ordinal);

        Metadata = new ReadOnlyDictionary<string, string>(copy);
    }

    public ResourceKind Kind { get; }

    public TIdentity Identity { get; }

    public PrincipalId Owner { get; }

    public ResourceScope Scope { get; }

    public ResourceVersion Version { get; }

    public ResourceProvenance Provenance { get; }

    public ResourceLifecycle Lifecycle { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public ResourceIdentitySnapshot<TIdentity> Snapshot() =>
        new(Kind, Identity, Version);

    public ResourceEnvelope<TIdentity> TransitionLifecycle(
        ResourceLifecycleStatus status,
        DateTimeOffset changedAtUtc)
    {
        return new(
            Kind,
            Identity,
            Owner,
            Scope,
            Version.Next(),
            Provenance,
            Lifecycle.TransitionTo(status, changedAtUtc),
            Metadata);
    }

    public ResourceEnvelope<TIdentity> WithMetadata(
        string key,
        string value)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata key is required.", nameof(key));

        ArgumentNullException.ThrowIfNull(value);

        var copy = new Dictionary<string, string>(Metadata, StringComparer.Ordinal)
        {
            [key] = value
        };

        return new(
            Kind,
            Identity,
            Owner,
            Scope,
            Version.Next(),
            Provenance,
            Lifecycle,
            copy);
    }
}
