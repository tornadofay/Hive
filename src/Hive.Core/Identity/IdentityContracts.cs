namespace Hive.Core;

internal static class IdentityValue
{
    public static Guid Require(Guid value, string name) =>
        value == Guid.Empty
            ? throw new ArgumentException($"{name} cannot be empty.", name)
            : value;

    public static Guid Parse(string value, string name, string typeName) =>
        Guid.TryParse(value, out var parsed) && parsed != Guid.Empty
            ? parsed
            : throw new FormatException($"Invalid {typeName}: '{value}'.");

    public static bool TryParse(string? value, out Guid result)
    {
        if (Guid.TryParse(value, out var parsed) && parsed != Guid.Empty)
        {
            result = parsed;
            return true;
        }

        result = default;
        return false;
    }
}

public readonly record struct DeploymentId
{
    public DeploymentId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static DeploymentId New() => new(Guid.NewGuid());
    public static DeploymentId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(DeploymentId)));
    public static bool TryParse(string? value, out DeploymentId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct TenantId
{
    public TenantId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(TenantId)));
    public static bool TryParse(string? value, out TenantId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct PrincipalId
{
    public PrincipalId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static PrincipalId New() => new(Guid.NewGuid());
    public static PrincipalId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(PrincipalId)));
    public static bool TryParse(string? value, out PrincipalId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct UserId
{
    public UserId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static UserId New() => new(Guid.NewGuid());
    public static UserId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(UserId)));
    public static bool TryParse(string? value, out UserId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct SessionId
{
    public SessionId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static SessionId New() => new(Guid.NewGuid());
    public static SessionId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(SessionId)));
    public static bool TryParse(string? value, out SessionId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct WorkspaceId
{
    public WorkspaceId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static WorkspaceId New() => new(Guid.NewGuid());
    public static WorkspaceId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(WorkspaceId)));
    public static bool TryParse(string? value, out WorkspaceId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct AgentId
{
    public AgentId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static AgentId New() => new(Guid.NewGuid());
    public static AgentId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(AgentId)));
    public static bool TryParse(string? value, out AgentId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct HiveId
{
    public HiveId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static HiveId New() => new(Guid.NewGuid());
    public static HiveId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(HiveId)));
    public static bool TryParse(string? value, out HiveId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct RuntimeId
{
    public RuntimeId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static RuntimeId New() => new(Guid.NewGuid());
    public static RuntimeId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(RuntimeId)));
    public static bool TryParse(string? value, out RuntimeId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct ExecutionId
{
    public ExecutionId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static ExecutionId New() => new(Guid.NewGuid());
    public static ExecutionId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(ExecutionId)));
    public static bool TryParse(string? value, out ExecutionId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public readonly record struct WorkItemId
{
    public WorkItemId(Guid value) => Value = IdentityValue.Require(value, nameof(value));
    public Guid Value { get; }
    public static WorkItemId New() => new(Guid.NewGuid());
    public static WorkItemId Parse(string value) => new(IdentityValue.Parse(value, nameof(value), nameof(WorkItemId)));
    public static bool TryParse(string? value, out WorkItemId result)
    {
        if (IdentityValue.TryParse(value, out var parsed)) { result = new(parsed); return true; }
        result = default; return false;
    }
    public override string ToString() => Value.ToString("D");
}

public enum ResourceKind
{
    Deployment,
    Tenant,
    Principal,
    User,
    Session,
    Workspace,
    Agent,
    Hive,
    Runtime,
    Execution,
    WorkItem,
    Provider,
    ProviderAccount,
    ExecutionTarget,
    Secret
}
