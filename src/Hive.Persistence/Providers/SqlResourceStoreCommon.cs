using System.Text.Json;
using Hive.Core;
using Microsoft.Data.SqlClient;

namespace Hive.Persistence;

internal static class SqlResourceStoreCommon
{
    internal const string ScopeAccessPredicate = """
        (
            [ScopeKind] = @GlobalScope
            OR ([ScopeKind] = @TenantScope AND @TenantId IS NOT NULL AND [ScopeIdentity] = @TenantId)
            OR ([ScopeKind] = @UserScope AND @UserId IS NOT NULL AND [ScopeIdentity] = @UserId)
            OR ([ScopeKind] = @WorkspaceScope AND @WorkspaceId IS NOT NULL AND [ScopeIdentity] = @WorkspaceId)
            OR ([ScopeKind] = @AgentScope AND @AgentId IS NOT NULL AND [ScopeIdentity] = @AgentId)
            OR ([ScopeKind] = @RuntimeScope AND @RuntimeId IS NOT NULL AND [ScopeIdentity] = @RuntimeId)
            OR ([ScopeKind] = @ExecutionScope AND @ExecutionId IS NOT NULL AND [ScopeIdentity] = @ExecutionId)
        )
        """;

    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General);

    internal static IReadOnlyDictionary<string, string> DeserializeMetadata(
        string json)
    {
        var value = JsonSerializer.Deserialize<Dictionary<string, string>>(
            json,
            JsonOptions);

        return value is null
            ? throw new InvalidOperationException(
                "Persisted metadata JSON is invalid.")
            : new Dictionary<string, string>(
                value,
                StringComparer.Ordinal);
    }



    internal static string SerializeMetadata(
        IReadOnlyDictionary<string, string> metadata) =>
        JsonSerializer.Serialize(metadata, JsonOptions);


    internal static void AddAccessParameters(
        SqlCommand command,
        ResourceAccessContext accessContext)
    {
        command.Parameters.Add(
            GuidParameter(
                "@PrincipalId",
                accessContext.PrincipalId!.Value.Value));
        command.Parameters.Add(
            GuidParameter(
                "@TenantId",
                accessContext.TenantId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@UserId",
                accessContext.UserId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@WorkspaceId",
                accessContext.WorkspaceId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@AgentId",
                accessContext.AgentId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@RuntimeId",
                accessContext.RuntimeId?.Value));
        command.Parameters.Add(
            GuidParameter(
                "@ExecutionId",
                accessContext.ExecutionId?.Value));
        command.Parameters.Add(
            IntParameter(
                "@GlobalScope",
                (int)ResourceScopeKind.Global));
        command.Parameters.Add(
            IntParameter(
                "@TenantScope",
                (int)ResourceScopeKind.Tenant));
        command.Parameters.Add(
            IntParameter(
                "@UserScope",
                (int)ResourceScopeKind.User));
        command.Parameters.Add(
            IntParameter(
                "@WorkspaceScope",
                (int)ResourceScopeKind.Workspace));
        command.Parameters.Add(
            IntParameter(
                "@AgentScope",
                (int)ResourceScopeKind.Agent));
        command.Parameters.Add(
            IntParameter(
                "@RuntimeScope",
                (int)ResourceScopeKind.Runtime));
        command.Parameters.Add(
            IntParameter(
                "@ExecutionScope",
                (int)ResourceScopeKind.Execution));
    }




}
